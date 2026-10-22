using System.Collections.Frozen;
using TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Framework.MarketData.OptionChainCache;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;

namespace TomasAI.IFM.Application.MarketData.OptionChainCache;

/// <summary>API-resident facade over provider-independent storage; all read operations are bounded local CPU work.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1724", Justification = "Preserve the published type name and its CLR identity.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0049", Justification = "Preserve the published domain type and protocol identity.")]
public sealed class OptionChainCache : IOptionChainCache, IOptionChainSnapshotPublisher, IDisposable
{
    sealed record Prepared(MarketCompositionSnapshot Snapshot, DateOnly ValueDate, string ConfigurationDigest,
        FrozenDictionary<string, CompositionInstrumentSnapshot> Contracts, StrategyOptionChainParameterSet? Policy);
    readonly OptionChainSnapshotStore<Prepared> store;
    readonly TimeProvider clock;
    readonly DatasetWorkerAdmissionRegistry? admissions;
    readonly System.Threading.Lock lifecycle = new();
    Guid admittedGeneration;
    readonly Dictionary<string, OptionChainPublication> publications = new(StringComparer.Ordinal);
    public event Action<OptionChainPublication>? Published;
    public OptionChainPublication[] Export()
    { lock (lifecycle) return publications.Values.Where(x => x.Snapshot.ValidUntilUtc > clock.GetUtcNow()).ToArray(); }


    /// <summary>Creates a bounded cache with an injectable clock for deterministic freshness verification.</summary>
    public OptionChainCache(TimeProvider? time = null, int maximumScopes = 128, DatasetWorkerAdmissionRegistry? admissions = null)
    {
        clock = time ?? TimeProvider.System;
        store = new(maximumScopes);
        this.admissions = admissions;
        if (admissions is not null) { admissions.Changed += AdmissionChanged; AdmissionChanged(); }
    }

    /// <inheritdoc />
    public void Admit(Guid generation)
    {
        lock (lifecycle)
        {
            if (admissions is not null && (!admissions.TryGet("GLBX.MDP3", out var current) || current.GenerationId != generation)) return;
            store.Admit(generation); if (admittedGeneration != generation) publications.Clear(); admittedGeneration = generation;
        }
    }

    void AdmissionChanged()
    {
        lock (lifecycle)
        {
            if (admissions!.TryGet("GLBX.MDP3", out var current))
            {
                if (admittedGeneration != current.GenerationId) { store.Admit(current.GenerationId); publications.Clear(); admittedGeneration = current.GenerationId; }
            }
            else { store.Fence(admittedGeneration); publications.Clear(); admittedGeneration = Guid.Empty; }
        }
    }

    /// <summary>Detaches the admission listener and invalidates its last admitted generation.</summary>
    public void Dispose()
    {
        if (admissions is not null) admissions.Changed -= AdmissionChanged;
        lock (lifecycle) { store.Fence(admittedGeneration); publications.Clear(); admittedGeneration = Guid.Empty; }
    }

    /// <inheritdoc />
    public OptionChainCacheStatus GetStatus(OptionChainCacheScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (string.IsNullOrWhiteSpace(scope.ScopeId)) return new(false, 0, null, null, 0, "InvalidScope");
        var publication = store.Read(scope.GenerationId, scope.ScopeId);
        if (publication is null) return new(false, 0, null, null, 0, "ScopeNotReady");
        var snapshot = publication.Snapshot.Snapshot;
        return new(snapshot.ValidUntilUtc > clock.GetUtcNow(), publication.Version, publication.PublishedAtUtc, snapshot.ValidUntilUtc, snapshot.Instruments.Length,
            snapshot.ValidUntilUtc <= clock.GetUtcNow() ? "SnapshotExpired" : "");
    }
    /// <inheritdoc />
    public void Retire(Guid generation, string lookupScope)
    { lock (lifecycle) { store.Remove(generation, lookupScope); if (generation == admittedGeneration) publications.Remove(lookupScope); } }
    /// <inheritdoc />
    public void Fence(Guid generation)
    { lock (lifecycle) { store.Fence(generation); if (generation == admittedGeneration) { publications.Clear(); admittedGeneration = Guid.Empty; } } }

    /// <inheritdoc />
    public bool Publish(MarketCompositionSnapshot snapshot, DateOnly valueDate, string configurationDigest, long version, string? lookupScope = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationDigest);
        if (valueDate == default || snapshot.Instruments.IsDefault || snapshot.Instruments.Length > 2048
            || snapshot.SnapshotId == Guid.Empty || snapshot.GenerationId == Guid.Empty || snapshot.SchemaVersion != 1
            || snapshot.EvaluatedAtUtc.Offset != TimeSpan.Zero || snapshot.ValidUntilUtc <= snapshot.EvaluatedAtUtc
            || !string.Equals(snapshot.Digest,PricingSemanticHash.Compute(snapshot with { Digest = "" }),StringComparison.Ordinal))
            throw new ArgumentException("Invalid prepared snapshot.", nameof(snapshot));
        var policy = snapshot.StrategyOptionChainParameters ?? (snapshot.StrategyOptionChainParametersJson is null ? null : StrategyOptionChainParameterSet.Read(snapshot.StrategyOptionChainParametersJson));
        if (policy is not null && (snapshot.StrategyOptionChainValueDate != valueDate || !string.Equals(policy.Hash(),configurationDigest,StringComparison.Ordinal) || !policy.Enabled
            || !policy.BiasRows.Any(x => x.Enabled && string.Equals(x.MarketBias.ToString(),snapshot.StrategyOptionChainBias,StringComparison.Ordinal))))
            throw new ArgumentException("Prepared global policy provenance does not match its publication.", nameof(snapshot));
        foreach (var row in snapshot.Instruments)
        {
            if (row.Valuation is not { } value || !double.IsFinite(value.ImpliedVolatility) || value.ImpliedVolatility <= 0
                || !double.IsFinite(value.Delta) || !double.IsFinite(value.Gamma) || !double.IsFinite(value.Theta)
                || !double.IsFinite(value.Vega) || !double.IsFinite(value.Rho) || !double.IsFinite(value.TheoreticalPrice)
                || !double.IsFinite(value.TimeToExpiry) || value.TimeToExpiry <= 0 || value.TheoreticalPrice < 0
                || value.ContextDigest is not { Length: 64 } || !value.ContextDigest.All(Uri.IsHexDigit))
                throw new ArgumentException("Full finite pricing provenance is required for prepared snapshots.", nameof(snapshot));
        }
        var applied = store.Publish(snapshot.GenerationId, lookupScope ?? snapshot.ScopeId,
            new(version, clock.GetUtcNow(), new(snapshot, valueDate, configurationDigest,
                snapshot.Instruments.ToFrozenDictionary(x => x.Instrument.ContractId, StringComparer.Ordinal), policy)));
        OptionChainCacheTelemetry.Publications.Add(1, new KeyValuePair<string, object?>("accepted", applied));
        if (applied)
        {
            var publication = new OptionChainPublication(snapshot, valueDate, configurationDigest, version, lookupScope ?? snapshot.ScopeId);
            lock (lifecycle)
            {
                if (admittedGeneration == snapshot.GenerationId)
                {
                    if (!publications.TryGetValue(publication.LookupScope, out var prior) || prior.Version < version)
                        publications[publication.LookupScope] = publication;
                    Published?.Invoke(publication);
                }
            }
            OptionChainCacheTelemetry.PublicationAge.Record((clock.GetUtcNow() - snapshot.EvaluatedAtUtc).TotalMilliseconds);
        }
        return applied;
    }

    /// <inheritdoc />
    public OptionChainSnapshotResult TryGetSnapshot(OptionChainSnapshotRequest request)
    {
        if (!OptionChainCacheTelemetry.ReadMilliseconds.Enabled && !OptionChainCacheTelemetry.ReadOutcomes.Enabled) return Read(request);
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var result = Read(request);
        OptionChainCacheTelemetry.ReadMilliseconds.Record(System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        OptionChainCacheTelemetry.ReadOutcomes.Add(1, new KeyValuePair<string, object?>("reason", result.IsReady ? "Ready" : result.ReasonCode));
        return result;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3267", Justification = "Keep this bounded explicit loop to avoid additional iterator allocations and preserve early-exit or sequential asynchronous processing in the market-data path.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep native resource ownership and recovery failure ordering together during this storage compatibility refactor.")]
    OptionChainSnapshotResult Read(OptionChainSnapshotRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = clock.GetUtcNow();
        if (string.IsNullOrWhiteSpace(request.ScopeId)
            || request.ValueDate == default || request.Horizon is not ("Daily" or "Weekly" or "Monthly")
            || request.DeadlineUtc.Offset != TimeSpan.Zero || request.DeadlineUtc <= now
            || string.IsNullOrWhiteSpace(request.ConfigurationDigest) || request.RequiredContractIds.IsDefault
            || !string.Equals(request.InstrumentRoot,"ES",StringComparison.Ordinal) || request.StrategyDefinitionVersion < 0
            || request.StrategyDefinitionId != Guid.Empty && request.StrategyDefinitionVersion < 1
            || request.RequiredContractIds.Length > 4 || request.MaximumQuoteAgeMilliseconds is < 1 or > 5000
            || request.MaximumQuoteSkewMilliseconds is < 0 or > 2000)
            return new(OptionChainSnapshotOutcome.Rejected, "InvalidSnapshotRequest");
        for (var i = 0; i < request.RequiredContractIds.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(request.RequiredContractIds[i])) return new(OptionChainSnapshotOutcome.Rejected, "InvalidSnapshotRequest");
            for (var j = 0; j < i; j++)
                if (string.Equals(request.RequiredContractIds[i],request.RequiredContractIds[j],StringComparison.Ordinal)) return new(OptionChainSnapshotOutcome.Rejected, "InvalidSnapshotRequest");
        }
        if (!string.IsNullOrEmpty(request.FundPortfolioParameterSchema))
            return new(OptionChainSnapshotOutcome.Rejected, "UnsupportedFundPortfolioParameters");
        var publication = store.Read(request.GenerationId, request.ScopeId);
        if (publication is null) return new(OptionChainSnapshotOutcome.Deferred, "ScopeNotReady");
        var prepared = publication.Snapshot;
        var snapshot = prepared.Snapshot;
        OptionChainSnapshotResult Unavailable(string reason) => new(OptionChainSnapshotOutcome.Deferred, reason, ChainVersion: publication.Version);
        if (prepared.ValueDate != request.ValueDate || !string.Equals(prepared.ConfigurationDigest,request.ConfigurationDigest,StringComparison.Ordinal)
            || !string.Equals(snapshot.Horizon,request.Horizon,StringComparison.Ordinal)) return Unavailable("PolicyNotReady");
        if (request.StrategyDefinitionId != Guid.Empty && (prepared.Policy is not { } pinned
            || pinned.StrategyDefinitionId != request.StrategyDefinitionId || pinned.StrategyDefinitionVersion != request.StrategyDefinitionVersion
            || !string.Equals(pinned.InstrumentRoot,request.InstrumentRoot,StringComparison.Ordinal))) return Unavailable("StrategyNotReady");
        if (snapshot.ValidUntilUtc <= now || snapshot.EvaluatedAtUtc > now) return Unavailable("SnapshotExpired");
        var earliest = DateTimeOffset.MaxValue;
        var latest = DateTimeOffset.MinValue;
        var count = 0;
        var maximumSkew = request.MaximumQuoteSkewMilliseconds;
        foreach (var id in request.RequiredContractIds)
            if (!prepared.Contracts.ContainsKey(id)) return Unavailable("CoverageIncomplete");
        // Return the full ranking snapshot only when every returned instrument remains usable.
        // Required IDs demand coverage; they never authorize leaking unchecked instruments to the composer.
        foreach (var item in snapshot.Instruments)
        {
            count++;
            var instrument = item.Instrument;
            if (instrument.Quote is not { } quote || instrument.Underlying is not { } underlying
                || instrument.Pricing is null || item.Valuation is null) return Unavailable("ValuationUnavailable");
            var context = instrument.Pricing;
            if (!string.Equals(context.Contract.ContractId,instrument.ContractId,StringComparison.Ordinal) || !string.Equals(quote.ContractId,instrument.ContractId,StringComparison.Ordinal)
                || !string.Equals(underlying.ContractId,context.Contract.UnderlyingContractId,StringComparison.Ordinal) || instrument.Strike is not > 0
                || instrument.IsCall is null || context.Contract.ExpirationUtc <= now || context.Contract.LastTradingUtc <= now
                || context.MaximumQuoteAgeMilliseconds is < 1 or > 5000 || context.MaximumQuoteSkewMilliseconds is < 0 or > 2000
                || context.MaximumSourceClockLeadMilliseconds is < 0 or > 2000)
                return Unavailable("PricingContextUnavailable");
            if (context.GenerationId != snapshot.GenerationId || quote.GenerationId != snapshot.GenerationId
                || underlying.GenerationId != snapshot.GenerationId || context.ValidUntilUtc <= now)
                return Unavailable("PricingContextUnavailable");
            maximumSkew = Math.Min(maximumSkew, context.MaximumQuoteSkewMilliseconds);
            var maximumAge = Math.Min(request.MaximumQuoteAgeMilliseconds, context.MaximumQuoteAgeMilliseconds);
            bool Stale(OptionPricingQuote observation) => observation.EventAtUtc > now.AddMilliseconds(context.MaximumSourceClockLeadMilliseconds) || observation.ReceivedAtUtc > now
                || (now - observation.EventAtUtc).TotalMilliseconds > maximumAge
                || (now - observation.ReceivedAtUtc).TotalMilliseconds > maximumAge;
            if (Stale(quote) || Stale(underlying)) return Unavailable("QuoteStale");
            if (quote.Bid < 0 || quote.Ask <= 0 || quote.Ask < quote.Bid || quote.BidSize <= 0 || quote.AskSize <= 0
                || quote.EventAtUtc.Offset != TimeSpan.Zero || quote.ReceivedAtUtc.Offset != TimeSpan.Zero
                || underlying.EventAtUtc.Offset != TimeSpan.Zero || underlying.ReceivedAtUtc.Offset != TimeSpan.Zero
                || underlying.Bid <= 0 || underlying.Ask < underlying.Bid || underlying.BidSize <= 0 || underlying.AskSize <= 0) return Unavailable("InvalidQuote");
            if (quote.EventAtUtc < earliest) earliest = quote.EventAtUtc;
            if (underlying.EventAtUtc < earliest) earliest = underlying.EventAtUtc;
            if (quote.EventAtUtc > latest) latest = quote.EventAtUtc;
            if (underlying.EventAtUtc > latest) latest = underlying.EventAtUtc;
        }
        if (count == 0) return new(OptionChainSnapshotOutcome.NoTrade, "NoEligibleCandidate", ChainVersion: publication.Version);
        if ((latest - earliest).TotalMilliseconds > maximumSkew) return Unavailable("IncoherentQuotes");
        return new(OptionChainSnapshotOutcome.Ready, "", snapshot, publication.Version);
    }
}
