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
    readonly Guid generation;
    readonly DateOnly valueDate;
    readonly string dataset;
    readonly TimeProvider clock;
    readonly IDatabentoLastPriceReaderProvider prices;
    readonly OptionChainPricingInputStore inputs = new();
    readonly OptionChainStateStore state = new();
    readonly DatabentoOptionChainSessionManager sessions;
    readonly SemaphoreSlim gate = new(1);
    readonly Dictionary<string, Scope> scopes = new(StringComparer.Ordinal);
    readonly HashSet<Guid> endedLeases = [];
    readonly Dictionary<string, (string AuthorityScope, long Revision, string Digest)> ownershipWatermarks = new(StringComparer.Ordinal);
    readonly CancellationTokenSource stopping = new();
    readonly Task expiryLoop;
    readonly Action<string>? terminalFault;
    int disposed;

    public WorkerOptionChainRuntime(Guid generation, DateOnly valueDate, IDatabentoFeedFactory feeds,
        DatabentoFeedOptions options, ITickAggregationService aggregation, IDatabentoLastPriceStore prices,
        TimeProvider? time = null, Action<string>? terminalFault = null)
    {
        if (generation == Guid.Empty) throw new ArgumentException("A dataset generation is required.");
        this.generation = generation; this.valueDate = valueDate; dataset = options.Dataset;
        this.prices = prices; clock = time ?? TimeProvider.System;
        this.terminalFault = terminalFault;
        sessions = new(feeds, options, aggregation, prices,
            new Black76OptionChainGreeksEnricher(inputs, generation, clock, ReadUnderlying), new TransientSink(), state);
        expiryLoop = ExpireAsync();
    }

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
            if (request.GenerationId != generation || request.ValueDate != valueDate) return Failure("Recovering");
            if (string.IsNullOrWhiteSpace(request.ScopeId) || request.ScopeId.Length > 128 || request.LeaseId == Guid.Empty
                || request.LeaseExpiresAtUtc.Offset != TimeSpan.Zero || request.LeaseExpiresAtUtc <= at
                || request.LeaseExpiresAtUtc > at.AddSeconds(120) || request.Options.IsDefaultOrEmpty
                || request.Options.Length > 512 || MessagePackBinarySerializer.MeasureContent(request) > 524288)
                return Failure("InvalidChainRequest");
            if (endedLeases.Contains(request.LeaseId)) return Failure("LeaseEnded");
            if (endedLeases.Count + scopes.Values.Sum(s => s.Leases.Count) >= 4096
                && !scopes.Values.Any(s => s.Leases.ContainsKey(request.LeaseId))) return Failure("LeaseCapacity");
            if (scopes.Any(s => s.Key != request.ScopeId && s.Value.Leases.ContainsKey(request.LeaseId))) return Failure("LeaseIdentityConflict");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var instruments = new HashSet<InstrumentKey>();
            string? underlying = null;
            foreach (var option in request.Options)
            {
                if (option?.Pricing is not { } context) return Failure("PricingContextUnavailable");
                var failure = Black76PricingModel.ValidateContext(context, at);
                if (failure is not null) return new(false, failure);
                var contract = context.Contract;
                if (context.GenerationId != generation || contract.Dataset != dataset || option.Strike <= 0
                    || !ids.Add(contract.ContractId) || !instruments.Add(new(contract.PublisherId, contract.InstrumentId))
                    || underlying is not null && underlying != contract.UnderlyingContractId
                    || DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(contract.ExpirationUtc,
                        TimeZoneInfo.FindSystemTimeZoneById(context.Calendar.TimeZoneId)).DateTime) != request.MaturityDate)
                    return Failure("ConflictingDefinition");
                underlying = contract.UnderlyingContractId;
            }
            var ordered = request.Options.OrderBy(x => x.Pricing.Contract.ContractId, StringComparer.Ordinal).ToImmutableArray();
            var digest = PricingSemanticHash.Compute(new { request.MaturityDate, Options = ordered });
            if (scopes.TryGetValue(request.ScopeId, out var existing))
            {
                if (existing.Leases.Count >= 128 && !existing.Leases.ContainsKey(request.LeaseId)) return Failure("LeaseCapacity");
                if (state.GetSession(existing.Key).Count != ordered.Length) return Failure("ChainUnavailable");
                if (existing.Digest != digest)
                {
                    // Compare-and-swap the entire pricing batch. A refresh cannot redefine physical routes,
                    // revive an ended lease, or silently overwrite a concurrent reference update.
                    if (request.ExpectedContextDigest != existing.Digest
                        || !existing.Leases.ContainsKey(request.LeaseId) && !existing.BusinessOwners.Any(x => x.LeaseId == request.LeaseId)
                        || PhysicalDigest(existing.Options) != PhysicalDigest(ordered)
                        || ordered.Where((option, index) => option.Pricing.MaximumQuoteAgeMilliseconds > existing.Options[index].Pricing.MaximumQuoteAgeMilliseconds
                            || option.Pricing.MaximumQuoteSkewMilliseconds > existing.Options[index].Pricing.MaximumQuoteSkewMilliseconds).Any())
                        return Failure("ConflictingChainScope");
                    var latest = ReadUnderlying(underlying!);
                    if (latest is null) return Failure("UnderlyingQuoteUnavailable");
                    foreach (var option in ordered) inputs.Set(new(option.Pricing, latest));
                    existing.Options = ordered;
                    existing.Digest = digest;
                }
                if (!existing.BusinessOwners.Any(x => x.LeaseId == request.LeaseId))
                    existing.Leases[request.LeaseId] = request.LeaseExpiresAtUtc;
                return new(true, null, digest);
            }
            var key = new OptionChainSessionKey(underlying!, request.MaturityDate);
            // A physical session has one canonical scope; a second scope cannot alter or release it.
            if (scopes.Values.Any(s => s.Key == key)) return Failure("ConflictingChainScope");
            if (scopes.Count >= 8 || scopes.Values.Sum(s => s.Options.Length) + ordered.Length > 2048) return Failure("ChainCapacity");
            var quote = ReadUnderlying(underlying!);
            if (quote is null || quote.Bid <= 0 || quote.Ask < quote.Bid || quote.EventAtUtc > at
                || quote.ReceivedAtUtc > at || ordered.Any(o => (at - quote.EventAtUtc).TotalMilliseconds > o.Pricing.MaximumQuoteAgeMilliseconds))
                return Failure("UnderlyingQuoteUnavailable");
            var routes = ordered.Select(o => new DatabentoOptionChainRoute
            {
                FuturesOptionContractId = o.Pricing.Contract.ContractId,
                Definition = Definition(o, request.MaturityDate)
            }).ToArray();
            foreach (var option in ordered) inputs.Set(new(option.Pricing, quote));
            try
            {
                await sessions.StartAsync(new()
                {
                    FuturesContractId = underlying!, ValueDate = valueDate, Routes = routes,
                    Subscription = new()
                    {
                        Underlying = underlying!, MaturityDate = request.MaturityDate,
                        Strikes = ordered.Select(x => x.Strike).Distinct().Order().ToArray(),
                        ResolvedContracts = routes.Select(x => x.Definition).ToArray()
                    }
                }, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                scopes.Add(request.ScopeId, new(key, digest, ordered, request.LeaseId, request.LeaseExpiresAtUtc));
                return new(true, null, digest);
            }
            catch
            {
                try { await sessions.StopAsync(underlying!, request.MaturityDate).ConfigureAwait(false); }
                finally { foreach (var option in ordered) inputs.Remove(option.Pricing.Contract.ContractId); }
                throw;
            }
        }
        finally { gate.Release(); }
    }

    public async Task<WorkerOptionChainResult> ReleaseAsync(WorkerOptionChainRelease release, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (release.GenerationId != generation) return Failure("Recovering");
            if (!scopes.TryGetValue(release.ScopeId, out var scope)) return new(false, null);
            if (release.Ownership is { } ownership)
            {
                if (release.LeaseId != Guid.Empty || ownership.ContractSetDigest != PhysicalDigest(scope.Options)
                    || ownership.Revision < 0 || string.IsNullOrWhiteSpace(ownership.AuthorityScope) || ownership.AuthorityScope.Length > 128
                    || ownership.Owners.IsDefault || ownership.Owners.Length > 128
                    || ownership.Owners.Any(x => x is null || x.LeaseId == Guid.Empty || x.ContractIds.IsDefaultOrEmpty || x.ContractIds.Length > 128
                        || x.ContractIds.Distinct(StringComparer.Ordinal).Count() != x.ContractIds.Length
                        || x.ContractIds.Any(id => !scope.Options.Any(option => option.Pricing.Contract.ContractId == id)))
                    || ownership.Owners.Select(x => x.LeaseId).Distinct().Count() != ownership.Owners.Length)
                    return Failure("OwnershipInvalid");
                var digest = PricingSemanticHash.Compute(ownership);
                if (ownershipWatermarks.TryGetValue(release.ScopeId, out var prior)
                    && (prior.AuthorityScope != ownership.AuthorityScope || ownership.Revision < prior.Revision
                        || ownership.Revision == prior.Revision && digest != prior.Digest))
                    return Failure("OwnershipConflict");
                if (ownershipWatermarks.Count >= 4096 && !ownershipWatermarks.ContainsKey(release.ScopeId))
                    return Failure("OwnershipCapacity");
                if (scope.AuthorityScope is not null && scope.AuthorityScope != ownership.AuthorityScope
                    || ownership.Revision < scope.OwnershipRevision
                    || ownership.Revision == scope.OwnershipRevision && digest != scope.OwnershipDigest)
                    return Failure("OwnershipConflict");
                scope.AuthorityScope = ownership.AuthorityScope;
                scope.OwnershipRevision = ownership.Revision;
                scope.OwnershipDigest = digest;
                scope.BusinessOwners = ownership.Owners;
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
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await RemoveExpiredAsync().ConfigureAwait(false);
            if (request.GenerationId != generation || continuation is not null || !request.IncludeOptions
                || !scopes.TryGetValue(request.ScopeId, out var scope)) throw new CompositionMarketSourceException("ChainUnavailable");
            var current = state.GetSession(scope.Key).ToDictionary(x => x.Route.FuturesOptionContractId, StringComparer.Ordinal);
            if (current.Count != scope.Options.Length) throw new CompositionMarketSourceException("IncompleteChain");
            var underlying = ReadUnderlying(scope.Key.FuturesContractId)
                ?? throw new CompositionMarketSourceException("UnderlyingQuoteUnavailable");
            var values = ImmutableArray.CreateBuilder<CompositionMarketInstrument>(scope.Options.Length);
            foreach (var option in scope.Options)
            {
                var id = option.Pricing.Contract.ContractId;
                if (!current.TryGetValue(id, out var item) || item.Quote is not { } quote)
                    throw new CompositionMarketSourceException("QuoteUnavailable");
                values.Add(new(id, Convert(quote.Tick), option.Pricing, option.Strike, option.IsCall, underlying));
            }
            return new(scope.Digest, generation, values.Count, values.MoveToImmutable(), null);
        }
        finally { gate.Release(); }
    }

    OptionPricingQuote? ReadUnderlying(string id)
    {
        return prices.GetFuturesReader(id, valueDate).TryGetLastQuote(out var quote)
            && quote.BidPrice is not null && quote.AskPrice is not null ? Convert(quote) : null;
    }

    OptionPricingQuote Convert(LastQuoteTickSnapshot quote) => new(quote.ContractId,
        quote.BidPrice ?? 0, quote.AskPrice ?? 0, quote.BidSize, quote.AskSize,
        quote.EventTimestamp, quote.ReceiveTimestamp, quote.SourceSequence, generation);

    static OptionContractDefinition Definition(WorkerOptionDefinition option, DateOnly maturity)
    {
        var c = option.Pricing.Contract;
        return new()
        {
            Dataset = c.Dataset, RawSymbol = c.RawSymbol, Ticker = c.Root, Underlying = c.UnderlyingContractId,
            Instrument = new(c.PublisherId, c.InstrumentId), Right = option.IsCall ? OptionRightSelection.Call : OptionRightSelection.Put,
            StrikePrice = option.Strike, MaturityDate = maturity,
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
        catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
        catch (Exception exception)
        {
            terminalFault?.Invoke($"Option-chain lease drain failed: {exception.GetType().Name}");
            throw;
        }
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
        try { await sessions.StopAsync(scope.Key.FuturesContractId, scope.Key.MaturityDate).ConfigureAwait(false); }
        catch (Exception exception)
        {
            terminalFault?.Invoke($"Option-chain shutdown failed: {exception.GetType().Name}");
            throw;
        }
        finally { foreach (var option in scope.Options) inputs.Remove(option.Pricing.Contract.ContractId); }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        await stopping.CancelAsync().ConfigureAwait(false);
        try { await expiryLoop.ConfigureAwait(false); }
        finally
        {
            await gate.WaitAsync().ConfigureAwait(false);
            try { await sessions.DisposeAsync().ConfigureAwait(false); scopes.Clear(); }
            finally { gate.Release(); }
            stopping.Dispose();
        }
    }

    static WorkerOptionChainResult Failure(string code) => new(false, new(code, "Chain", "", "Qualified worker chain admission failed."));
    /// <summary>Reference rates/calendars may refresh; the exact native subscription identity may not.</summary>
    public static string PhysicalDigest(ImmutableArray<WorkerOptionDefinition> options) => PricingSemanticHash.Compute(
        options.OrderBy(x => x.Pricing.Contract.ContractId, StringComparer.Ordinal).Select(x => new
        {
            x.Strike, x.IsCall, x.Pricing.Contract.ContractId, x.Pricing.Contract.Dataset,
            x.Pricing.Contract.PublisherId, x.Pricing.Contract.InstrumentId, x.Pricing.Contract.RawSymbol,
            x.Pricing.Contract.UnderlyingContractId, x.Pricing.Contract.ExpirationUtc,
            x.Pricing.Contract.DefinitionDigest
        }).ToArray());
    sealed class Scope(OptionChainSessionKey key, string digest, ImmutableArray<WorkerOptionDefinition> options, Guid leaseId, DateTimeOffset expires)
    {
        public OptionChainSessionKey Key { get; } = key;
        public string Digest { get; set; } = digest;
        public ImmutableArray<WorkerOptionDefinition> Options { get; set; } = options;
        public Dictionary<Guid, DateTimeOffset> Leases { get; } = new() { [leaseId] = expires };
        public string? AuthorityScope { get; set; }
        public long OwnershipRevision { get; set; } = -1;
        public string? OwnershipDigest { get; set; }
        public ImmutableArray<WorkerOptionChainOwner> BusinessOwners { get; set; } = [];
    }
    sealed class TransientSink : IOptionChainTransientEventPublisher
    {
        public ValueTask PublishAsync(FuturesOptionChainQuoteChangedServiceEvent value) => ValueTask.CompletedTask;
        public ValueTask PublishAsync(FuturesOptionChainTradeChangedServiceEvent value) => ValueTask.CompletedTask;
    }
}

public sealed class CompositionMarketSourceException(string code) : Exception(code)
{
    public string Code { get; } = code;
}
