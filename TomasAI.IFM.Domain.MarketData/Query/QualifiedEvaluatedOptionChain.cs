using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Query;
static class QualifiedEvaluatedOptionChain
{
    static readonly ConcurrentDictionary<string, WorkerOptionChainRequest> Leases = new(StringComparer.Ordinal);
    static readonly ConcurrentDictionary<string, CachedWindowInputs> WindowInputs = new(StringComparer.Ordinal);
    static readonly ConcurrentDictionary<string, AtTheMoneyIv> ImpliedVolatility = new(StringComparer.Ordinal);
    static readonly ConcurrentDictionary<string, SemaphoreSlim> ReferencePublicationGates = new(StringComparer.Ordinal);
    static readonly SemaphoreSlim[] ScopeGates = Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1, 1)).ToArray();
    static readonly ConcurrentDictionary<string, Dictionary<string, OwnerRequest>> Owners = new(StringComparer.Ordinal);
    sealed record OwnerRequest(DateTimeOffset Expires, string[] Required);
    static SemaphoreSlim ScopeGate(string key) => ScopeGates[(uint)StringComparer.Ordinal.GetHashCode(key) % 64];
    static readonly ConcurrentDictionary<(string ContractId, string MappingVersion), byte> PublishedReferences = new();
    sealed record CachedWindowInputs(Guid GenerationId, DateTimeOffset ExpiresAtUtc, string[] ProviderRoots, FuturesOptionContractReadModel[] Definitions, decimal? Price, decimal? Deviation);
    sealed record AtTheMoneyIv(Guid GenerationId, DateTimeOffset ObservedAtUtc, double Value);
    /// <summary>Returns a qualified, expiry-specific live chain, using ATM IV when available and a labelled Bollinger fallback otherwise.</summary>
    public static async Task<ServiceResult<EvaluatedOptionChainReadModel>> ExecuteAsync(GetEvaluatedOptionChainQuery query, IMarketDataQueryContext context, CancellationToken token)
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var result = await ExecuteCoreAsync(query, context, token).ConfigureAwait(false);
        var diagnosticLogger = context.Logger;
        if (diagnosticLogger.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Information))
        {
            var logOutcome = result.Success ? "Succeeded" : "Rejected";
var logElapsedMilliseconds = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
global::TomasAI.IFM.Domain.MarketData.StorageMigrationDiagnosticLog.Write4(diagnosticLogger, null, nameof(QualifiedEvaluatedOptionChain), nameof(ExecuteAsync), query.UnderlyingContractId, query.ExpiryDate, query.FrozenEmulatorPreviewOnly, query.ReleaseOnly, logOutcome, result.ErrorCode, logElapsedMilliseconds);
        }

        return result;
    }

    static async Task<ServiceResult<EvaluatedOptionChainReadModel>> ExecuteCoreAsync(GetEvaluatedOptionChainQuery query, IMarketDataQueryContext context, CancellationToken token)
    {
        var api = context.MarketDataApi;
        var discovery = context.CompositionDiscovery;
        var market = context.CompositionMarketData;
        var admissions = context.WorkerAdmissions;
        if (query.AllowFrozenEmulatorPreview && !query.ReleaseOnly && (query.FrozenEmulatorPreviewOnly || api?.GetRuntimeStatus().ActiveValueDate is null))
        {
            return await (FrozenEmulatorOptionChain.ExecuteAsync(query, context, token)).ConfigureAwait(false);
        }

        if (api is null || discovery is null || market is null || admissions is null)
        {
            return new ServiceFailed<EvaluatedOptionChainReadModel>(503, "Qualified market-data runtime is unavailable.");
        }

        var key = $"{query.UnderlyingContractId}|{query.ExpiryDate:yyyyMMdd}";
        var gate = ScopeGate(key);
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (query.ReleaseOnly)
            {
                return await (ReleaseAsync(key, query, discovery, token)).ConfigureAwait(false);
            }

            if (!admissions.TryGet("GLBX.MDP3", out var admission))
            {
                return new ServiceFailed<EvaluatedOptionChainReadModel>(503, "Market-data worker is not admitted.");
            }

            var owners = Owners.GetOrAdd(key, _ => new(StringComparer.Ordinal));
            foreach (var owner in owners.Where(x => x.Value.Expires <= DateTimeOffset.UtcNow).Select(x => x.Key).ToArray())
                owners.Remove(owner);
            owners[query.SubscriptionOwnerId ?? ""] = new(DateTimeOffset.UtcNow.AddSeconds(90), query.RequiredContractIds);
            // A serialized scope mutation prevents concurrent required-leg requests from replacing each other.
            var effective = query with
            {
                RequiredContractIds = owners.Values.SelectMany(x => x.Required).Distinct(StringComparer.Ordinal).ToArray()
            };
            return await (AcquireAsync(key, effective, context, api, discovery, market, admission, token)).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    static async Task<ServiceResult<EvaluatedOptionChainReadModel>> ReleaseAsync(string key, GetEvaluatedOptionChainQuery query, QualifiedCompositionDiscovery discovery, CancellationToken token)
    {
        if (Owners.TryGetValue(key, out var owners))
        {
            owners.Remove(query.SubscriptionOwnerId ?? "");
            if (owners.Count > 0)
            {
                return new ServiceOk<EvaluatedOptionChainReadModel>(new(query.UnderlyingContractId, query.ExpiryDate, null, null, null, "Released", DateTimeOffset.UtcNow, []));
            }

            Owners.TryRemove(key, out _);
        }

        // Keep reusable metadata after the final owner leaves, but release the live lease.
        if (Leases.TryRemove(key, out var lease))
        {
            await (discovery.ReleaseAsync(lease, token)).ConfigureAwait(false);
        }

        return new ServiceOk<EvaluatedOptionChainReadModel>(new(query.UnderlyingContractId, query.ExpiryDate, null, null, null, "Released", DateTimeOffset.UtcNow, []));
    }

    static async Task<ServiceResult<EvaluatedOptionChainReadModel>> AcquireAsync(string key, GetEvaluatedOptionChainQuery query, IMarketDataQueryContext context, TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi api, QualifiedCompositionDiscovery discovery, ICompositionMarketDataApi market, DatasetWorkerAdmission admission, CancellationToken token)
    {
        var now = DateTimeOffset.UtcNow;
        var roots = query.ProviderRoots.Order(StringComparer.OrdinalIgnoreCase).ToArray();
        var cached = WindowInputs.TryGetValue(key, out var existing) && existing.GenerationId == admission.GenerationId && existing.ExpiresAtUtc > now && existing.ProviderRoots.SequenceEqual(roots, StringComparer.OrdinalIgnoreCase) ? existing : null;
        decimal? livePrice;
        var metadataStarted = System.Diagnostics.Stopwatch.GetTimestamp();
        global::TomasAI.IFM.Domain.MarketData.ActorLifecycleDiagnosticLog.Write5(context.Logger, null, nameof(QualifiedEvaluatedOptionChain), nameof(AcquireAsync), query.UnderlyingContractId, query.ExpiryDate, admission.GenerationId, cached is not null);
        if (cached is null)
        {
            var definitionsTask = LoadDefinitionsAsync(query, context, token);
            var inputsTask = OptionChainWindowInputs.GetAsync(context, query.UnderlyingSymbol, query.UnderlyingContractId, token);
            await Task.WhenAll(definitionsTask, inputsTask).ConfigureAwait(false);
            var definitions = await (definitionsTask).ConfigureAwait(false);
            var inputs = await (inputsTask).ConfigureAwait(false);
            livePrice = inputs.Price;
            cached = new(admission.GenerationId, now.AddSeconds(30), roots, definitions, inputs.Price, inputs.Deviation);
            WindowInputs[key] = cached;
        }
        else
        {
            livePrice = await api.GetFuturesPriceAsync(query.UnderlyingContractId).ConfigureAwait(false) ?? cached.Price;
        }

        global::TomasAI.IFM.Domain.MarketData.ActorLifecycleDiagnosticLog.Write6(context.Logger, null, key, query.SubscriptionOwnerId, existing is not null && ReferenceEquals(existing, cached), cached.Definitions.Length, System.Diagnostics.Stopwatch.GetElapsedTime(metadataStarted).TotalMilliseconds);
        var windowPrice = livePrice ?? cached.Price;
        var expiry = cached.Definitions.Where(x => x.ExpirationUtc is not null).Select(x => x.ExpirationUtc!.Value).DefaultIfEmpty().Min();
        var window = windowPrice is> 0 && expiry > now && ImpliedVolatility.TryGetValue(key, out var iv) && iv.GenerationId == admission.GenerationId && now - iv.ObservedAtUtc < TimeSpan.FromMinutes(5) ? OptionChainStrikeWindow.SelectImpliedVolatility(cached.Definitions, windowPrice.Value, iv.Value, expiry, now, requiredContractIds: query.RequiredContractIds) : OptionChainStrikeWindow.Select(cached.Definitions, windowPrice, cached.Deviation, query.StandardDeviationMultiplier, query.RequiredContractIds);
        if (window.Contracts.Length == 0)
        {
            return Failed(string.Equals(window.Method, "WindowInputsUnavailable", StringComparison.Ordinal) ? "Neither a qualified expiry IV nor current Bollinger window inputs are available." : "No contracts are available in the selected strike window.");
        }

        var coverage = OptionChainSubscriptionCoverage.Buffer(cached.Definitions, window, query.SpreadWingWidth);
        var subscriptionStarted = System.Diagnostics.Stopwatch.GetTimestamp();
        var lease = await (EnsureLeaseAsync(key, query, context, discovery, admission, window.Contracts, coverage, token)).ConfigureAwait(false);
        global::TomasAI.IFM.Domain.MarketData.ActorLifecycleDiagnosticLog.Write7(context.Logger, null, key, admission.GenerationId, query.SubscriptionOwnerId, window.Contracts.Length, coverage.Length, System.Diagnostics.Stopwatch.GetElapsedTime(subscriptionStarted).TotalMilliseconds);
        if (lease.Lease is null)
        {
            global::TomasAI.IFM.Domain.MarketData.ActorLifecycleDiagnosticLog.Write8(context.Logger, null, query.UnderlyingContractId, query.ExpiryDate, lease.FailureCode);
            return Failed($"The qualified option-chain lease could not be acquired: {lease.FailureCode}.");
        }

        return await (CaptureAsync(key, query, market, lease.Lease, window, livePrice ?? windowPrice, token)).ConfigureAwait(false);
    }

    static async Task<FuturesOptionContractReadModel[]> LoadDefinitionsAsync(GetEvaluatedOptionChainQuery query, IMarketDataQueryContext context, CancellationToken token)
    {
        var cached = await (context.DbFactory.SecuritiesDb.GetCachedOptionContractDefinitionsAsync(query.UnderlyingSymbol, query.UnderlyingContractId, query.ExpiryDate, query.ProviderRoots, token)).ConfigureAwait(false);
        return cached.Select(row => row.Definition).DistinctBy(x => x.ContractId).ToArray();
    }

    internal static async Task PublishSelectedReferencesAsync(string key, IMarketDataQueryContext context, FuturesOptionContractReadModel[] contracts, CancellationToken token) => await PublishSelectedReferencesAsync(key, context.DbFactory.SecuritiesDb, context.Logger, contracts, token).ConfigureAwait(false);
    internal static async Task PublishSelectedReferencesAsync(string key, TomasAI.IFM.Application.Storage.SecuritiesDb.ISecuritiesDbContext securities, Microsoft.Extensions.Logging.ILogger logger, FuturesOptionContractReadModel[] contracts, CancellationToken token)
    {
        var gate = ReferencePublicationGates.GetOrAdd(key, static _ => new(1, 1));
        await (gate.WaitAsync(token)).ConfigureAwait(false);
        try
        {
            var selected = contracts.DistinctBy(static contract => (contract.ContractId, contract.MappingVersion)).ToArray();
            await (Parallel.ForEachAsync(selected, new ParallelOptions { CancellationToken = token, MaxDegreeOfParallelism = Math.Min(8, selected.Length) }, async (definition, cancellationToken) =>
            {
                if (definition.ReviewState != ReferenceReviewState.Reviewed || string.IsNullOrWhiteSpace(definition.MappingVersion))
                {
                    throw new InvalidDataException($"Selected option definition '{definition.ContractId}' is not reviewed.");
                }

                var referenceKey = (definition.ContractId, definition.MappingVersion);
                if (PublishedReferences.ContainsKey(referenceKey))
                {
                    return;
                }

                if (await securities.GetReferenceVersionAsync(definition.ContractId, definition.MappingVersion, cancellationToken).ConfigureAwait(false)is not null)
                {
                    PublishedReferences.TryAdd(referenceKey, 0);
                    return;
                }

                var pending = await securities.StageReferenceVersionAsync(definition, cancellationToken).ConfigureAwait(false);
                await securities.CommitReferenceVersionAsync(pending, cancellationToken).ConfigureAwait(false);
                PublishedReferences.TryAdd(referenceKey, 0);
            })).ConfigureAwait(false);
            global::TomasAI.IFM.Domain.MarketData.ActorLifecycleDiagnosticLog.Write9(logger, null, key, selected.Length);
        }
        finally
        {
            gate.Release();
        }
    }

    static async Task<(WorkerOptionChainRequest? Lease, string? FailureCode)> EnsureLeaseAsync(string key, GetEvaluatedOptionChainQuery query, IMarketDataQueryContext context, QualifiedCompositionDiscovery discovery, DatasetWorkerAdmission admission, FuturesOptionContractReadModel[] requested, FuturesOptionContractReadModel[] contracts, CancellationToken token)
    {
        var now = DateTimeOffset.UtcNow;
        if (Leases.TryGetValue(key, out var current) && current.GenerationId == admission.GenerationId && current.LeaseExpiresAtUtc > now.AddSeconds(10) && Covers(current, requested))
        {
            return (current, null);
        }

        if (current is not null && current.GenerationId == admission.GenerationId && current.LeaseExpiresAtUtc > now && Covers(current, requested))
        {
            var renewed = await (discovery.RenewAsync(current, now.AddSeconds(60), token)).ConfigureAwait(false);
            if (renewed is not null)
            {
                Leases[key] = renewed;
                return (renewed, null);
            }

            return (current, null);
        }

        var replacingSameContracts = current is not null && current.GenerationId == admission.GenerationId && current.LeaseExpiresAtUtc > now && Covers(current, requested);
        if (current is not null && !replacingSameContracts)
        {
            Leases.TryRemove(key, out _);
            await (discovery.ReleaseAsync(current, token)).ConfigureAwait(false);
        }

        global::TomasAI.IFM.Domain.MarketData.ActorLifecycleDiagnosticLog.Write10(context.Logger, null, key, admission.GenerationId, (current is null) switch
        {
            true => "ColdStart",
            false => current.GenerationId != admission.GenerationId ? "WorkerGenerationChanged" : "CoverageOrLeaseExpired"
        }, contracts.Length);
        await (PublishSelectedReferencesAsync(key, context, contracts, token)).ConfigureAwait(false);
        var request = await (DiscoveryRequestAsync(query, context, admission, contracts, now, token)).ConfigureAwait(false);
        if (request is null)
        {
            return (null, "DiscoveryRequestUnavailable");
        }

        var result = await (discovery.AcquireAsync(request, token)).ConfigureAwait(false);
        if (result.Failure is not null || result.Lease is null)
        {
            return (null, result.Failure?.Code ?? "NoQualifiedDefinitions");
        }

        Leases[key] = result.Lease;
        if (replacingSameContracts)
        {
            await (discovery.ReleaseAsync(current!, token)).ConfigureAwait(false);
        }

        return (result.Lease, null);
    }

    static bool Covers(WorkerOptionChainRequest lease, FuturesOptionContractReadModel[] requested) => OptionChainSubscriptionCoverage.Covers(lease.Options.ToDictionary(x => x.Pricing.Contract.ContractId, x => (x.Pricing.Contract.MappingVersion, x.Pricing.Contract.DefinitionDigest), StringComparer.Ordinal), requested);
    static async Task<CompositionDiscoveryRequest?> DiscoveryRequestAsync(GetEvaluatedOptionChainQuery query, IMarketDataQueryContext context, DatasetWorkerAdmission admission, FuturesOptionContractReadModel[] contracts, DateTimeOffset now, CancellationToken token)
    {
        if (context.TreasuryPublication is null || context.TreasuryConversion is null)
        {
            return null;
        }

        var dates = await (context.DbFactory.MarketDataDb.GetTradingDatesAsync(admission.ValueDate, query.ExpiryDate, MarketType.Futures, CurrencyType.USD, token)).ConfigureAwait(false);
        var first = contracts[0];
        var calendar = new OptionPricingCalendar(first.CalendarVersion ?? "IFM-MarketDates", first.ExchangeTimeZoneId ?? "America/New_York", admission.ValueDate, query.ExpiryDate, new TimeOnly(18, 0), dates.ToImmutableArray());
        return new(Guid.NewGuid(), admission.GenerationId, admission.ValueDate, query.ExpiryDate, now.AddSeconds(60), contracts.Select(Candidate).ToArray(), true, calendar, context.TreasuryPublication, context.TreasuryConversion);
    }

    internal static OptionDefinitionCandidate Candidate(FuturesOptionContractReadModel value)
    {
        if (value.PublisherId is null || value.InstrumentId is null || value.ExpirationUtc is null || value.MappingVersion is null || value.DefinitionDigest is null || value.RawSymbol is null || value.Dataset is null || value.UnderlyingContractId is null)
        {
            throw new InvalidDataException("Option definition is not reviewed for live pricing.");
        }

        if (value.OptionRight is not (ReferenceOptionRight.Call or ReferenceOptionRight.Put))
        {
            throw new InvalidDataException("Option definition has no qualified call/put right.");
        }

        var definition = new OptionContractDefinition
        {
            Dataset = value.Dataset,
            RawSymbol = value.RawSymbol,
            Ticker = value.Symbol,
            Underlying = value.UnderlyingContractId,
            Instrument = new(value.PublisherId.Value, value.InstrumentId.Value),
            Right = value.OptionRight == ReferenceOptionRight.Call ? OptionRightSelection.Call : OptionRightSelection.Put,
            StrikePrice = value.GetExactStrikePrice(),
            MaturityDate = DateOnly.FromDateTime(value.ExpirationUtc.Value.UtcDateTime),
            ExpirationTimestampNanoseconds = ToNanoseconds(value.ExpirationUtc.Value),
            ContractMultiplier = value.MultiplierValue is null ? null : checked((int)value.MultiplierValue.Value)
        };
        return new(value.ContractId, value.MappingVersion, value.DefinitionDigest, definition);
    }

    static ulong ToNanoseconds(DateTimeOffset value) => checked((ulong)(value.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) * 100UL);
    static async Task<ServiceResult<EvaluatedOptionChainReadModel>> CaptureAsync(string key, GetEvaluatedOptionChainQuery query, ICompositionMarketDataApi market, WorkerOptionChainRequest lease, OptionChainStrikeWindowResult window, decimal? price, CancellationToken token)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var now = DateTimeOffset.UtcNow;
            var request = new CompositionSnapshotRequest(Guid.NewGuid(), lease.ScopeId, "Daily", lease.GenerationId, now, deadline, true)
            {
                AllowMissingOptionQuotes = true,
                SelectionOnly = true,
                RiskContractIds = query.RequiredContractIds
            };
            var captured = await (market.CaptureAsync("GLBX.MDP3", request, token)).ConfigureAwait(false);
            if (captured.Snapshot is not null)
            {
                ObserveAtTheMoneyIv(key, captured.Snapshot, price);
                return Success(query, captured.Snapshot, window, price);
            }

            if (captured.Failure?.Code is not ("QuoteUnavailable" or "UnderlyingQuoteUnavailable"))
            {
                return Failed(captured.Failure?.Code ?? "Qualified snapshot is unavailable.");
            }

            await (Task.Delay(50, token)).ConfigureAwait(false);
        }

        return Failed("Qualified snapshot timed out.");
    }

    static void ObserveAtTheMoneyIv(string key, MarketCompositionSnapshot snapshot, decimal? price)
    {
        if (price is not > 0)
        {
            return;
        }

        // A live option quote may update every second; hold the window's ATM IV for a
        // bounded interval so small IV ticks do not replace the physical subscription.
        if (ImpliedVolatility.TryGetValue(key, out var prior) && prior.GenerationId == snapshot.GenerationId && snapshot.EvaluatedAtUtc - prior.ObservedAtUtc < TimeSpan.FromSeconds(30))
        {
            return;
        }

        var iv = OptionChainAtmIvEstimator.Read(snapshot, price.Value);
        if (iv is not null)
            ImpliedVolatility[key] = new(snapshot.GenerationId, snapshot.EvaluatedAtUtc, iv.Value);
    }

    static ServiceResult<EvaluatedOptionChainReadModel> Success(GetEvaluatedOptionChainQuery query, MarketCompositionSnapshot snapshot, OptionChainStrikeWindowResult window, decimal? price)
    {
        var contracts = snapshot.Instruments.Select(ToReadModel).ToArray();
        var model = new EvaluatedOptionChainReadModel(query.UnderlyingContractId, query.ExpiryDate, price, window.LowerBound, window.UpperBound, window.Method, snapshot.EvaluatedAtUtc, contracts);
        return new ServiceOk<EvaluatedOptionChainReadModel>(model);
    }

    static ServiceResult<EvaluatedOptionChainReadModel> Failed(string message) => new ServiceFailed<EvaluatedOptionChainReadModel>(503, message);
    static EvaluatedOptionContractReadModel ToReadModel(CompositionInstrumentSnapshot item)
    {
        var instrument = item.Instrument;
        var quote = instrument.Quote;
        var value = item.Valuation;
        return new(instrument.ContractId, instrument.Strike!.Value, instrument.IsCall!.Value, quote?.Bid, quote?.Ask, quote is null ? null : checked((uint)quote.BidSize), quote is null ? null : checked((uint)quote.AskSize), null, null, value?.ImpliedVolatility ?? instrument.Selection?.ImpliedVolatility, value?.TheoreticalPrice ?? instrument.Selection?.Price, value?.Delta ?? instrument.Selection?.Delta, value?.Gamma, value?.Vega, value?.Theta, value?.Rho, instrument.SessionVolume, instrument.OpenInterest, value is not null, quote is null, quote?.EventAtUtc, null, instrument.Selection?.CalculatedAtUtc ?? (item.Valuation is null ? null : quote?.ReceivedAtUtc), SelectionValid: instrument.Selection is not null, QuoteUnavailableReason: instrument.QuoteUnavailableReason, LastQuoteEventAtUtc: instrument.LastQuoteEventAtUtc, LastQuoteReceivedAtUtc: instrument.LastQuoteReceivedAtUtc);
    }
}
