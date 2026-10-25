using System.Buffers;
using System.Collections.Immutable;
using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.DataBento.LastPrice;
using TomasAI.IFM.Framework.MarketData.DataBento.OptionChain;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>
/// Dataset-worker-owned physical chains. Temporary leases share exact scopes; expiry and release
/// drain the last physical owner. Durable business authority remains a host responsibility.
/// </summary>
public sealed class WorkerOptionChainRuntime : IAsyncDisposable, ICompositionMarketSource
{
    readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> individualContracts = new(StringComparer.Ordinal);
    readonly Guid generation;
    readonly DateOnly valueDate;
    readonly string dataset;
    readonly TimeProvider clock;
    readonly IDatabentoLastPriceReaderProvider prices;
    readonly OptionChainPricingInputStore inputs = new();
    readonly OptionChainStateStore state = new();
    readonly DatabentoOptionChainSessionManager sessions;
    readonly CoalescedOptionChainPricing pricing;
    readonly SemaphoreSlim gate = new(1);
    readonly Dictionary<string, Scope> scopes = new(StringComparer.Ordinal);
    readonly HashSet<Guid> endedLeases = [];
    readonly Dictionary<string, (string AuthorityScope, long Revision, string Digest)> ownershipWatermarks = new(StringComparer.Ordinal);
    readonly CancellationTokenSource stopping = new();
    readonly Task expiryLoop;
    readonly Action<string>? terminalFault;
    readonly OptionPricingRefreshPolicy refreshPolicy;
    readonly bool production;
    int disposed;

    public WorkerOptionChainRuntime(Guid generation, DateOnly valueDate, IDatabentoFeedFactory feeds,
        DatabentoFeedOptions options, ITickAggregationService aggregation, IDatabentoLastPriceStore prices,
        TimeProvider? time = null, Action<string>? terminalFault = null, OptionPricingRefreshPolicy? refreshPolicy = null,
        IOptionTradeEvidenceWriter? tradeEvidence = null,
        TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation.ITickAggregationEventPublisher? optionObservations = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (generation == Guid.Empty) throw new ArgumentException("A dataset generation is required.",nameof(generation));
        this.generation = generation; this.valueDate = valueDate; dataset = options.Dataset;
        this.prices = prices; clock = time ?? TimeProvider.System;
        this.terminalFault = terminalFault;
        this.refreshPolicy = refreshPolicy ?? new();
        this.refreshPolicy.Validate();
        production = options.DeploymentProfile == FeedDeploymentProfile.Production;
        var observations = optionObservations is null ? null
            : new IndividualOptionObservationPublisher(generation, inputs, optionObservations, IsMonitoredContract, clock);
        pricing = new(inputs, generation, this.refreshPolicy, ReadUnderlying, clock, terminalFault, tradeEvidence,
            observations is null ? null : observations.PublishPricingAsync, individualContracts.ContainsKey);
        sessions = new(feeds, options, aggregation, prices, pricing, optionObservations is null
            ? new TransientSink()
            : observations!, state, time: clock);
        expiryLoop = ExpireAsync();
    }
    bool IsMonitoredContract(string id) => individualContracts.ContainsKey(id) || pricing.IsBusinessRiskRequired(id);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep native resource ownership and recovery failure ordering together during this storage compatibility refactor.")]
    public async Task<WorkerOptionChainResult> AcquireAsync(WorkerOptionChainRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
            await RemoveExpiredAsync().ConfigureAwait(false);
            var at = clock.GetUtcNow();
            if (production && !refreshPolicy.IsProductionQualified) return Failure("PricingRefreshPolicyUnreviewed");
            if (request.GenerationId != generation || request.ValueDate != valueDate) return Failure("Recovering");
            if (string.IsNullOrWhiteSpace(request.ScopeId) || request.ScopeId.Length > 128 || request.LeaseId == Guid.Empty
                || request.LeaseExpiresAtUtc.Offset != TimeSpan.Zero || request.LeaseExpiresAtUtc <= at
                || request.LeaseExpiresAtUtc > at.AddSeconds(120) || request.Options.IsDefaultOrEmpty
                || request.Options.Length > 2048 || MessagePackBinarySerializer.MeasureContent(request) > 4 * 1024 * 1024)
                return Failure("InvalidChainRequest");
            if (endedLeases.Contains(request.LeaseId)) return Failure("LeaseEnded");
            if (endedLeases.Count + scopes.Values.Sum(s => s.Leases.Count) >= 4096
                && !scopes.Values.Any(s => s.Leases.ContainsKey(request.LeaseId))) return Failure("LeaseCapacity");
            if (scopes.Any(s => !string.Equals(s.Key,request.ScopeId,StringComparison.Ordinal) && s.Value.Leases.ContainsKey(request.LeaseId))) return Failure("LeaseIdentityConflict");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var instrumentIds = new HashSet<uint>();
            string? underlying = null;
            foreach (var option in request.Options)
            {
                if (option?.Pricing is not { } context) return Failure("PricingContextUnavailable");
                var failure = Black76PricingModel.ValidateContext(context, at);
                if (failure is not null) return new(false, failure);
                var contract = context.Contract;
                if (context.GenerationId != generation || !string.Equals(contract.Dataset,dataset,StringComparison.Ordinal) || option.Strike <= 0
                    || contract.SchemaVersion == 3 && (contract.Strike != option.Strike
                        || contract.Right != (option.IsCall ? PricingOptionRight.Call : PricingOptionRight.Put))
                    || !ids.Add(contract.ContractId) || !instrumentIds.Add(contract.InstrumentId)
                    || underlying is not null && !string.Equals(underlying,contract.UnderlyingContractId,StringComparison.Ordinal)
                    || DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(contract.ExpirationUtc,
                        TimeZoneInfo.FindSystemTimeZoneById(context.Calendar.TimeZoneId)).DateTime) != request.MaturityDate)
                    return Failure("ConflictingDefinition");
                underlying = contract.UnderlyingContractId;
            }
            if (request.SeparateContractConnection && (request.Options.Length != 1 || request.IndependentChainConnection)) return Failure("IndividualOptionRequired");
            var ordered = request.Options.OrderBy(x => x.Pricing.Contract.ContractId, StringComparer.Ordinal).ToImmutableArray();
            var digest = PricingSemanticHash.Compute(new { request.MaturityDate, request.SeparateContractConnection, Options = ordered });
            if (scopes.TryGetValue(request.ScopeId, out var existing))
            {
                if ((!string.IsNullOrEmpty(existing.Key.OptionContractId)) != request.SeparateContractConnection
                    || (!string.IsNullOrEmpty(existing.Key.ConnectionScopeId)) != request.IndependentChainConnection) return Failure("ConflictingConnectionMode");
                if (existing.Leases.Count >= 128 && !existing.Leases.ContainsKey(request.LeaseId)) return Failure("LeaseCapacity");
                if (!string.Equals(existing.Digest,digest,StringComparison.Ordinal))
                {
                    // Compare-and-swap the entire pricing batch. A refresh cannot redefine physical routes,
                    // revive an ended lease, or silently overwrite a concurrent reference update.
                    var conflict = !string.Equals(request.ExpectedContextDigest, existing.Digest, StringComparison.Ordinal) ? "Pricing context digest changed"
                        : !existing.Leases.ContainsKey(request.LeaseId) && !existing.BusinessOwners.Any(x => x.LeaseId == request.LeaseId) ? "Lease does not own this scope"
                        : !string.Equals(PhysicalDigest(existing.Options), PhysicalDigest(ordered), StringComparison.Ordinal) ? "Physical contract definitions changed"
                        : ordered.Where((option, index) => option.Pricing.MaximumQuoteAgeMilliseconds > existing.Options[index].Pricing.MaximumQuoteAgeMilliseconds
                            || option.Pricing.MaximumQuoteSkewMilliseconds > existing.Options[index].Pricing.MaximumQuoteSkewMilliseconds).Any() ? "Quote validity limits were relaxed" : null;
                    if (conflict is not null)
                        return new(false, new("ConflictingChainScope", "Chain", request.ScopeId,
                            $"{conflict}; ScopeId={request.ScopeId}; LeaseId={request.LeaseId}; ExpectedDigest={request.ExpectedContextDigest}; ActualDigest={existing.Digest}; RequestedDigest={digest}"));
                    var latest = ReadUnderlying(underlying!);
                    if (latest is null) return Failure("UnderlyingQuoteUnavailable");
                    foreach (var option in ordered) inputs.Set(new(option.Pricing, latest));
                    existing.Options = ordered;
                    existing.Digest = digest;
                }
                if (request.ReconnectExactContract)
                {
                    if (!request.SeparateContractConnection || existing.Options.Length != 1)
                        return Failure("ExactContractRepairRequired");
                    await sessions.StopAsync(existing.Key.FuturesContractId, existing.Key.MaturityDate,
                        existing.Key.OptionContractId, existing.Key.ConnectionScopeId).WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                await EnsureSessionAsync(existing, cancellationToken).ConfigureAwait(false);
                if (!existing.BusinessOwners.Any(x => x.LeaseId == request.LeaseId))
                    existing.Leases[request.LeaseId] = request.LeaseExpiresAtUtc;
                return new(true, null, digest);
            }
            var key = new OptionChainSessionKey(underlying!, request.MaturityDate,
                request.SeparateContractConnection ? ordered[0].Pricing.Contract.ContractId : "",
                request.IndependentChainConnection ? request.ScopeId : "");
            // A physical session has one canonical scope; a second scope cannot alter or release it.
            if (scopes.Values.Any(s => s.Key == key)) return new(false, new("ConflictingChainScope", "Chain", request.ScopeId,
                $"Physical subscription already belongs to another scope; ScopeId={request.ScopeId}; LeaseId={request.LeaseId}; Underlying={underlying}; Maturity={request.MaturityDate}"));
            if (scopes.Count >= 16 || scopes.Values.Sum(s => s.Options.Length) + ordered.Length > 2048) return Failure("ChainCapacity");
            var quote = ReadUnderlying(underlying!);
            if (quote is null || quote.Bid <= 0 || quote.Ask < quote.Bid
                || quote.ReceivedAtUtc > at || ordered.Any(o =>
                    quote.EventAtUtc > at.AddMilliseconds(o.Pricing.MaximumSourceClockLeadMilliseconds)
                    || (at - quote.EventAtUtc).TotalMilliseconds > o.Pricing.MaximumQuoteAgeMilliseconds
                    || (at - quote.ReceivedAtUtc).TotalMilliseconds > o.Pricing.MaximumQuoteAgeMilliseconds))
                return new(false, new("UnderlyingQuoteUnavailable", "Underlying", underlying!, quote is null
                    ? "No current underlying quote is cached."
                    : $"Underlying quote is invalid or stale; event age={(at - quote.EventAtUtc).TotalMilliseconds:F0}ms; receipt age={(at - quote.ReceivedAtUtc).TotalMilliseconds:F0}ms."));
            var routes = ordered.Select(o => new DatabentoOptionChainRoute
            {
                FuturesOptionContractId = o.Pricing.Contract.ContractId,
                Definition = Definition(o, request.MaturityDate)
            }).ToArray();
            foreach (var option in ordered) inputs.Set(new(option.Pricing, quote));
            if (request.SeparateContractConnection) individualContracts.TryAdd(key.OptionContractId, 0);
            try
            {
                foreach (var route in routes) pricing.Register(route);
                await sessions.StartAsync(new()
                {
                    FuturesContractId = underlying!,
                    OptionContractId = key.OptionContractId,
                ConnectionScopeId = key.ConnectionScopeId,
                    ValueDate = valueDate,
                    Routes = routes,
                    SavedStatistics = request.Statistics.IsDefault ? [] : request.Statistics,
                    Subscription = new()
                    {
                        Underlying = underlying!,
                        MaturityDate = request.MaturityDate,
                        Strikes = ordered.Select(x => x.Strike).Distinct().Order().ToArray(),
                        ResolvedContracts = routes.Select(x => x.Definition).ToArray(),
                        DataKinds = MarketDataKinds.Quote | MarketDataKinds.Trade | MarketDataKinds.Statistics | MarketDataKinds.SessionVolume
                    }
                }, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                scopes.Add(request.ScopeId, new(key, digest, ordered, request.LeaseId, request.LeaseExpiresAtUtc) { Statistics = request.Statistics });
                return new(true, null, digest);
            }
            catch
            {
                try { await sessions.StopAsync(underlying!, request.MaturityDate, key.OptionContractId, key.ConnectionScopeId).ConfigureAwait(false); }
                finally { foreach (var option in ordered) RemoveUnownedPricing(option.Pricing.Contract.ContractId); }
                throw;
            }
        }
        finally { gate.Release(); }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3267", Justification = "Keep this bounded explicit loop to avoid additional iterator allocations and preserve early-exit or sequential asynchronous processing in the market-data path.")]
    public async Task<WorkerOptionChainResult> ReleaseAsync(WorkerOptionChainRelease release, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(release);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (release.GenerationId != generation) return Failure("Recovering");
            if (!scopes.TryGetValue(release.ScopeId, out var scope)) return new(false, null);
            if (release.Ownership is { } ownership)
            {
                if (release.LeaseId != Guid.Empty || !string.Equals(ownership.ContractSetDigest,PhysicalDigest(scope.Options),StringComparison.Ordinal)
                    || ownership.Revision < 0 || string.IsNullOrWhiteSpace(ownership.AuthorityScope) || ownership.AuthorityScope.Length > 128
                    || ownership.Owners.IsDefault || ownership.Owners.Length > 128
                    || ownership.Owners.Any(x => x is null || x.LeaseId == Guid.Empty || x.ContractIds.IsDefaultOrEmpty || x.ContractIds.Length > 128
                        || x.ContractIds.Distinct(StringComparer.Ordinal).Count() != x.ContractIds.Length
                        || x.ContractIds.Any(id => !scope.Options.Any(option => string.Equals(option.Pricing.Contract.ContractId,id,StringComparison.Ordinal))))
                    || ownership.Owners.Select(x => x.LeaseId).Distinct().Count() != ownership.Owners.Length)
                    return Failure("OwnershipInvalid");
                var digest = PricingSemanticHash.Compute(ownership);
                if (ownershipWatermarks.TryGetValue(release.ScopeId, out var prior)
                    && (!string.Equals(prior.AuthorityScope,ownership.AuthorityScope,StringComparison.Ordinal) || ownership.Revision < prior.Revision
                        || ownership.Revision == prior.Revision && !string.Equals(digest,prior.Digest,StringComparison.Ordinal)))
                    return Failure("OwnershipConflict");
                if (ownershipWatermarks.Count >= 4096 && !ownershipWatermarks.ContainsKey(release.ScopeId))
                    return Failure("OwnershipCapacity");
                if (scope.AuthorityScope is not null && !string.Equals(scope.AuthorityScope,ownership.AuthorityScope,StringComparison.Ordinal)
                    || ownership.Revision < scope.OwnershipRevision
                    || ownership.Revision == scope.OwnershipRevision && !string.Equals(digest,scope.OwnershipDigest,StringComparison.Ordinal))
                    return Failure("OwnershipConflict");
                scope.AuthorityScope = ownership.AuthorityScope;
                scope.OwnershipRevision = ownership.Revision;
                scope.OwnershipDigest = digest;
                scope.BusinessOwners = ownership.Owners;
                foreach (var option in scope.Options)
                    pricing.RequireRisk(option.Pricing.Contract.ContractId,
                        ownership.Owners.Any(owner => owner.ContractIds.Contains(option.Pricing.Contract.ContractId, StringComparer.Ordinal)));
                ownershipWatermarks[release.ScopeId] = (ownership.AuthorityScope, ownership.Revision, digest);
                // This operation installs the complete business union. It never implicitly releases a discovery lease.
                if (scope.Leases.Count == 0 && scope.BusinessOwners.IsEmpty) await RemoveAsync(release.ScopeId, scope).ConfigureAwait(false);
                return new(scope.Leases.Count != 0 || !scope.BusinessOwners.IsEmpty, null, scope.Digest);
            }
            if (scope.Leases.Remove(release.LeaseId)) endedLeases.Add(release.LeaseId);
            if (scope.Leases.Count == 0 && scope.BusinessOwners.IsEmpty) await RemoveAsync(release.ScopeId, scope).ConfigureAwait(false);
            return new(scope.Leases.Count != 0 || !scope.BusinessOwners.IsEmpty, null);
        }
        finally { gate.Release(); }
    }

    public async Task<CompositionMarketPage> ReadAsync(CompositionSnapshotRequest request, string? continuation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await RemoveExpiredAsync().ConfigureAwait(false);
            if (request.GenerationId != generation || continuation is not null || !request.IncludeOptions
                || !scopes.TryGetValue(request.ScopeId, out var scope)) throw new CompositionMarketSourceException("ChainUnavailable");
            await EnsureSessionAsync(scope, cancellationToken).ConfigureAwait(false);
            var current = ArrayPool<OptionChainContractState>.Shared.Rent(scope.Options.Length);
            try
            {
                if (!state.TryCopyContracts(scope.Key, scope.ContractIds, current.AsSpan(0, scope.Options.Length)))
                    throw new CompositionMarketSourceException("IncompleteChain");
                var underlying = ReadUnderlying(scope.Key.FuturesContractId);
                if (underlying is null && !request.AllowMissingOptionQuotes)
                    throw new CompositionMarketSourceException("UnderlyingQuoteUnavailable");
                var values = ImmutableArray.CreateBuilder<CompositionMarketInstrument>(scope.Options.Length);
                for (var index = 0; index < scope.Options.Length; index++)
                {
                    var option = scope.Options[index];
                    var id = option.Pricing.Contract.ContractId;
                    var item = current[index];
                    if (request.SelectionOnly && !request.RiskContractIds.Contains(id, StringComparer.Ordinal))
                    {
                        var selection = pricing.ReadSelection(id);
                        if (selection is { Failure: null, Delta: not null, Iv: not null })
                        {
                            values.Add(new(id, selection.Option, option.Pricing, option.Strike, option.IsCall, selection.Underlying)
                            {
                                Selection = OptionSelectionValue.From(selection, refreshPolicy.ImpliedVolatilityMilliseconds),
                                SessionVolume = item.SessionVolume,
                                OpenInterest = item.OpenInterest,
                                StatisticsAtUtc = item.StatisticsAtUtc, VolumeValueDate = item.VolumeValueDate, OpenInterestValueDate = item.OpenInterestValueDate
                            });
                            continue;
                        }
                        if (!request.AllowMissingOptionQuotes)
                            throw new CompositionMarketSourceException(selection?.Failure?.Code ?? "SelectionCalculationPending");
                    }
                    if (item.Quote is not { } && !request.AllowMissingOptionQuotes)
                        throw new CompositionMarketSourceException("QuoteUnavailable");
                    values.Add(new(id, item.Quote is { } quote ? Convert(quote.Tick) : null, option.Pricing, option.Strike, option.IsCall, underlying)
                    { SessionVolume = item.SessionVolume, OpenInterest = item.OpenInterest, StatisticsAtUtc = item.StatisticsAtUtc, VolumeValueDate = item.VolumeValueDate, OpenInterestValueDate = item.OpenInterestValueDate });
                }
                return new(scope.Digest, generation, values.Count, values.MoveToImmutable(), null);
            }
            finally { ArrayPool<OptionChainContractState>.Shared.Return(current, clearArray: true); }
        }
        finally { gate.Release(); }
    }

    public ChainSelectionCalculation? ReadSelection(string contractId) => pricing.ReadSelection(contractId);
    public OptionPricingPassResult? ReadRisk(string contractId) => pricing.ReadRisk(contractId);

    OptionPricingQuote? ReadUnderlying(string id)
    {
        return prices.GetFuturesReader(id, valueDate).TryGetLastQuote(out var quote)
            && quote.BidPrice is not null && quote.AskPrice is not null ? Convert(quote) : null;
    }

    OptionPricingQuote Convert(LastQuoteTickSnapshot quote) => new(quote.ContractId,
        quote.BidPrice ?? 0, quote.AskPrice ?? 0, quote.BidSize, quote.AskSize,
        quote.EventTimestamp, quote.LocalReceivedAtUtc == default ? quote.ReceiveTimestamp : quote.LocalReceivedAtUtc,
        quote.SourceSequence, generation);

    static OptionContractDefinition Definition(WorkerOptionDefinition option, DateOnly maturity)
    {
        var c = option.Pricing.Contract;
        return new()
        {
            Dataset = c.Dataset,
            RawSymbol = c.RawSymbol,
            Ticker = c.Root,
            Underlying = c.UnderlyingContractId,
            Instrument = new(c.PublisherId, c.InstrumentId),
            Right = option.IsCall ? OptionRightSelection.Call : OptionRightSelection.Put,
            StrikePrice = option.Strike,
            MaturityDate = maturity,
            ExpirationTimestampNanoseconds = checked((ulong)(c.ExpirationUtc.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) * 100)
        };
    }

    async Task ExpireAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1), clock);
        try
        {
            while (await timer.WaitForNextTickAsync(stopping.Token).ConfigureAwait(false))
            {
                await gate.WaitAsync(stopping.Token).ConfigureAwait(false);
                try { await RemoveExpiredAsync().ConfigureAwait(false); }
                finally { gate.Release(); }
            }
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested) { /* Cancellation or optional observation must not abort the enclosing shutdown/recovery boundary. */ }
        catch (Exception exception)
        {
            terminalFault?.Invoke($"Option-chain lease drain failed: {exception}");
            throw;
        }
    }

    // Logical ownership survives a failed provider connection. Recreate only the exact
    // registered scope; do not release business owners or substitute a different contract set.
    async Task EnsureSessionAsync(Scope scope, CancellationToken token)
    {
        if (state.GetCount(scope.Key) == scope.Options.Length) return;
        await sessions.StopAsync(scope.Key.FuturesContractId, scope.Key.MaturityDate,
            scope.Key.OptionContractId, scope.Key.ConnectionScopeId).ConfigureAwait(false);
        var routes = scope.Options.Select(option => new DatabentoOptionChainRoute
        {
            FuturesOptionContractId = option.Pricing.Contract.ContractId,
            Definition = Definition(option, scope.Key.MaturityDate)
        }).ToArray();
        await sessions.StartAsync(new()
        {
            FuturesContractId = scope.Key.FuturesContractId,
            OptionContractId = scope.Key.OptionContractId,
            ConnectionScopeId = scope.Key.ConnectionScopeId,
            ValueDate = valueDate,
            Routes = routes,
            SavedStatistics = scope.Statistics.IsDefault ? [] : scope.Statistics,
            Subscription = new()
            {
                Underlying = scope.Key.FuturesContractId,
                MaturityDate = scope.Key.MaturityDate,
                Strikes = scope.Options.Select(x => x.Strike).Distinct().Order().ToArray(),
                ResolvedContracts = routes.Select(x => x.Definition).ToArray(),
                DataKinds = MarketDataKinds.Quote | MarketDataKinds.Trade | MarketDataKinds.Statistics | MarketDataKinds.SessionVolume
            }
        }, token).ConfigureAwait(false);
    }

    async Task RemoveExpiredAsync()
    {
        var at = clock.GetUtcNow();
        foreach (var (id, scope) in scopes.ToArray())
        {
            foreach (var lease in scope.Leases.Where(x => x.Value <= at).Select(x => x.Key).ToArray())
            { scope.Leases.Remove(lease); endedLeases.Add(lease); }
            if (scope.Leases.Count == 0 && scope.BusinessOwners.IsEmpty) await RemoveAsync(id, scope).ConfigureAwait(false);
        }
    }

    async Task RemoveAsync(string id, Scope scope)
    {
        scopes.Remove(id);
        try { await sessions.StopAsync(scope.Key.FuturesContractId, scope.Key.MaturityDate, scope.Key.OptionContractId, scope.Key.ConnectionScopeId).ConfigureAwait(false); }
        catch (Exception exception)
        {
            terminalFault?.Invoke($"Option-chain shutdown failed: {exception}");
            throw;
        }
        finally { foreach (var option in scope.Options) RemoveUnownedPricing(option.Pricing.Contract.ContractId); }
    }

    /// <summary>Releases cached pricing only after the last physical scope referencing the contract ends.</summary>
    /// <param name="contractId">The option whose final owner may have ended.</param>
    void RemoveUnownedPricing(string contractId)
    {
        if (scopes.Values.Any(scope => scope.Options.Any(option => string.Equals(option.Pricing.Contract.ContractId,contractId,StringComparison.Ordinal))))
            return;
        individualContracts.TryRemove(contractId, out _);
        inputs.Remove(contractId);
        pricing.Remove(contractId);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        await stopping.CancelAsync().ConfigureAwait(false);
        try { await expiryLoop.ConfigureAwait(false); }
        finally
        {
            await gate.WaitAsync().ConfigureAwait(false);
            try
            {
                try { await sessions.DisposeAsync().ConfigureAwait(false); }
                finally { await pricing.DisposeAsync().ConfigureAwait(false); scopes.Clear(); }
            }
            finally { gate.Release(); gate.Dispose(); }
            stopping.Dispose();
        }
    }

    static WorkerOptionChainResult Failure(string code) => new(false, new(code, "Chain", "", "Qualified worker chain admission failed."));
    /// <summary>Reference rates/calendars may refresh; the exact native subscription identity may not.</summary>
    public static string PhysicalDigest(ImmutableArray<WorkerOptionDefinition> options) => PricingSemanticHash.Compute(
        options.OrderBy(x => x.Pricing.Contract.ContractId, StringComparer.Ordinal).Select(x => new
        {
            x.Strike,
            x.IsCall,
            x.Pricing.Contract.ContractId,
            x.Pricing.Contract.Dataset,
            x.Pricing.Contract.InstrumentId,
            x.Pricing.Contract.RawSymbol,
            x.Pricing.Contract.UnderlyingContractId,
            x.Pricing.Contract.ExpirationUtc,
            x.Pricing.Contract.DefinitionDigest
        }).ToArray());
    sealed class Scope(OptionChainSessionKey key, string digest, ImmutableArray<WorkerOptionDefinition> options, Guid leaseId, DateTimeOffset expires)
    {
        public OptionChainSessionKey Key { get; } = key;
        public string Digest { get; set; } = digest;
        public ImmutableArray<TomasAI.IFM.Framework.MarketData.Contracts.OptionChainStatisticsSnapshot> Statistics { get; init; }
        ImmutableArray<WorkerOptionDefinition> optionsValue = options;
        public string[] ContractIds { get; private set; } = options.Select(x => x.Pricing.Contract.ContractId).ToArray();
        public ImmutableArray<WorkerOptionDefinition> Options
        {
            get => optionsValue;
            set { optionsValue = value; ContractIds = value.Select(x => x.Pricing.Contract.ContractId).ToArray(); }
        }
        public Dictionary<Guid, DateTimeOffset> Leases { get; } = new() { [leaseId] = expires };
        public string? AuthorityScope { get; set; }
        public long OwnershipRevision { get; set; } = -1;
        public string? OwnershipDigest { get; set; }
        public ImmutableArray<WorkerOptionChainOwner> BusinessOwners { get; set; } = [];
    }
    sealed class TransientSink : IOptionChainTransientEventPublisher
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the existing public contract and named argument compatibility; changing parameter names or order would break callers.")]
        public ValueTask PublishAsync(FuturesOptionChainQuoteChangedServiceEvent value) => ValueTask.CompletedTask;
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the existing public contract and named argument compatibility; changing parameter names or order would break callers.")]
        public ValueTask PublishAsync(FuturesOptionChainTradeChangedServiceEvent value) => ValueTask.CompletedTask;
    }
}
