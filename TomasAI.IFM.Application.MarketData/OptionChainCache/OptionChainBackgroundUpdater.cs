using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;

namespace TomasAI.IFM.Application.MarketData.OptionChainCache;

/// <summary>Published global scope, resolved outside workflow handling. Identity pins the complete reviewed universe.</summary>
public sealed record PreparedOptionUniverse(string ScopeId, string ConfigurationDigest,
    CompositionMarketDataPlan MarketData, ImmutableArray<string> Horizons)
{
    /// <summary>Exact published global policy; null is reserved for explicit reviewed legacy universes.</summary>
    public StrategyOptionChainParameterSet? Parameters { get; init; }
}

/// <summary>Loads published strategy universes from persisted projections on the background path only.</summary>
public interface IOptionUniverseSource
{
    /// <summary>Returns the bounded published union for the admitted operational value date.</summary>
    Task<ImmutableArray<PreparedOptionUniverse>> LoadAsync(DateOnly valueDate, CancellationToken cancellationToken);
}

/// <summary>Maintains independent strategy leases and prepared valuations. Consumers never call this service.</summary>
public sealed class OptionChainBackgroundUpdater(IOptionUniverseSource universes, QualifiedCompositionDiscovery discovery,
    ICompositionMarketDataApi market, DatasetWorkerAdmissionRegistry admissions, IOptionChainSnapshotPublisher publisher,
    ILogger<OptionChainBackgroundUpdater> logger, TimeProvider? time = null, OptionChainCoverageObservations? coverage = null) : BackgroundService
{
    readonly TimeProvider clock = time ?? TimeProvider.System;
    readonly Dictionary<string, ActiveScope> scopes = new(StringComparer.Ordinal);
    sealed record ActiveScope(PreparedOptionUniverse Universe, WorkerOptionChainRequest Lease);
    readonly HashSet<string> publishedLookups = new(StringComparer.Ordinal);
    long version;
    Guid generation;
    readonly Dictionary<string, string> lastFailures = new(StringComparer.Ordinal);
    ImmutableArray<PreparedOptionUniverse> publishedPlans = [];
    DateTimeOffset nextPolicyRead;

    /// <summary>Stable scope identity pinned by the normalized reviewed plan, independent of worker generations.</summary>
    public static string ScopeId(CompositionMarketDataPlan plan, string horizon) => PricingSemanticHash.Compute(new { Plan = plan, Horizon = horizon });

    /// <summary>Runs one bounded maintenance cycle. This entry point also supports integration verification.</summary>
    public async Task MaintainAsync(CancellationToken token)
    {
        if (!admissions.TryGet("GLBX.MDP3", out var admission))
        {
            if (generation != Guid.Empty) publisher.Fence(generation);
            generation = Guid.Empty; publishedLookups.Clear(); lastFailures.Clear();
            await ReleaseAllAsync(token).ConfigureAwait(false);
            return;
        }
        if (generation != admission.GenerationId)
        {
            if (generation != Guid.Empty) publisher.Fence(generation);
            await ReleaseAllAsync(token).ConfigureAwait(false);
            generation = admission.GenerationId; publishedLookups.Clear(); lastFailures.Clear();
            nextPolicyRead = default;
            publisher.Admit(generation);
        }
        if (nextPolicyRead <= clock.GetUtcNow())
        {
            nextPolicyRead = clock.GetUtcNow().AddSeconds(30);
            publishedPlans = await universes.LoadAsync(admission.ValueDate, token).ConfigureAwait(false);
        }
        var plans = publishedPlans;
        if (plans.IsDefault || plans.Length > 128) throw new InvalidDataException("Global option universe exceeds the configured scope capacity.");
        var desiredLookups = plans.SelectMany(u => u.Horizons.SelectMany(h => u.Parameters is null ? new[] { ScopeId(u.MarketData, h) }
            : u.Parameters.BiasRows.Where(b => b.Enabled).Select(b => StrategyOptionChainScope.Key(u.Parameters.ParameterSetId,
                u.Parameters.Version, u.MarketData.ValueDate, b.MarketBias.ToString(), h)))).ToHashSet(StringComparer.Ordinal);
        foreach (var old in publishedLookups.Where(x => !desiredLookups.Contains(x)).ToArray())
        { publisher.Retire(generation, old); publishedLookups.Remove(old); }
        var wanted = new HashSet<string>(StringComparer.Ordinal);
        var published = new HashSet<string>(StringComparer.Ordinal);
        var captures = new Dictionary<string, MarketCompositionSnapshot>(StringComparer.Ordinal);
        foreach (var universe in plans)
        {
            token.ThrowIfCancellationRequested();
            var plan = universe.MarketData;
            if (plan.Dataset != "GLBX.MDP3" || !plan.IncludeOptions || !plan.ScopeComplete || plan.ValueDate != admission.ValueDate
                || plan.Calendar is null || plan.Publication is null || plan.Conversion is null || !plan.Futures.IsEmpty
                || universe.Horizons.IsDefaultOrEmpty || universe.Horizons.Length > 3 || plan.Options.IsDefault || plan.Options.Length > 2048
                || universe.Horizons.Any(x => x is not ("Daily" or "Weekly" or "Monthly")))
                throw new InvalidDataException("Published option universe is not qualified for the admitted date.");
            var ownerScope = PricingSemanticHash.Compute(plan);
            wanted.Add(ownerScope);
            if (!scopes.TryGetValue(ownerScope, out var active))
            {
                var acquired = await discovery.AcquireAsync(new(Guid.NewGuid(), generation, plan.ValueDate, plan.MaturityDate,
                    clock.GetUtcNow().AddSeconds(60), plan.Options, true, plan.Calendar, plan.Publication, plan.Conversion, IndependentChainConnection: true),
                    token, refreshAutomatically: true, renewAutomatically: true).ConfigureAwait(false);
                if (acquired.Failure is not null || acquired.Lease is null)
                {
                    Report(universe.ScopeId, acquired.Failure?.Code ?? "EmptyUniverse");
                    continue;
                }
                // Acquire replacement coverage first; a UI owner never owns this lease.
                var replacement = new ActiveScope(universe, acquired.Lease);
                scopes[ownerScope] = replacement;
                if (active is not null) await discovery.ReleaseAsync(active.Lease, token).ConfigureAwait(false);
                active = replacement;
            }
            if (active.Lease.LeaseExpiresAtUtc <= clock.GetUtcNow().AddSeconds(30))
            {
                var renewed = await discovery.RenewAsync(active.Lease, clock.GetUtcNow().AddSeconds(120), token).ConfigureAwait(false);
                if (renewed is null) { scopes.Remove(ownerScope); Report(universe.ScopeId, "LeaseUnavailable"); continue; }
                active = active with { Lease = renewed }; scopes[ownerScope] = active;
            }
                if (!admissions.TryGet("GLBX.MDP3", out var current) || current != admission) return;
            if (!captures.TryGetValue(ownerScope, out var raw))
            {
                var captured = await market.CaptureAsync("GLBX.MDP3", new(Guid.NewGuid(), active.Lease.ScopeId, universe.Horizons[0],
                    generation, default, clock.GetUtcNow().AddSeconds(2), true) { AllowMissingOptionQuotes = true }, token).ConfigureAwait(false);
                if (captured.Snapshot is not { } capturedSnapshot) { Report(universe.ScopeId, captured.Failure?.Code ?? "SnapshotUnavailable"); continue; }
                raw = capturedSnapshot; captures.Add(ownerScope, raw);
            }
                // Only pre-valued executable observations enter the prepared ranking scope. No calculation runs on a cache read.
                var eligible = raw.Instruments.Where(x => x.Valuation is not null && x.Instrument.Quote is not null
                    && x.Instrument.Underlying is not null && x.Instrument.Pricing is not null).ToImmutableArray();
                if (universe.Parameters is { } parameters && !eligible.IsEmpty)
                    coverage?.Observe(parameters.ParameterSetId, generation, eligible.Max(x => x.Valuation!.ImpliedVolatility),
                        Interlocked.Increment(ref version), clock.GetUtcNow());
                if (eligible.IsEmpty) { Report(universe.ScopeId, "NoQualifiedQuotes"); continue; }
            foreach (var horizon in universe.Horizons)
            {
                var dte = plan.MaturityDate.DayNumber - plan.ValueDate.DayNumber;
                var biases = universe.Parameters?.BiasRows.Where(x => x.Enabled && dte >= x.MinimumDte && dte <= x.MaximumDte).ToArray();
                foreach (var bias in biases ?? new OptionStrategyBiasParameters?[] { null })
                {
                    var lookup = bias is null ? ScopeId(plan, horizon) : StrategyOptionChainScope.Key(
                        universe.Parameters!.ParameterSetId, universe.Parameters.Version, plan.ValueDate, bias.MarketBias.ToString(), horizon);
                    // Preferred expirations are visited first. A fallback may publish only if preferred coverage is unavailable.
                    if (published.Contains(lookup)) continue;
                    var rows = bias is null ? eligible : OptionChainCandidatePreparation.Select(eligible, bias, clock.GetUtcNow());
                    if (rows.IsEmpty) { Report(lookup, "CandidateCoverageUnavailable"); continue; }
                    var until = rows.Min(x => MinimumExpiry(x.Instrument));
                    if (until <= clock.GetUtcNow()) { Report(lookup, "QuoteStale"); continue; }
                    // ScopeId remains the durable provider pricing plan used by subscription handoff.
                    var snapshot = raw with { Horizon = horizon, Instruments = rows, ValidUntilUtc = until, Digest = "",
                        StrategyOptionChainParametersJson = universe.Parameters?.Serialize(), StrategyOptionChainBias = bias?.MarketBias.ToString(), StrategyOptionChainValueDate = bias is null ? null : plan.ValueDate };
                    snapshot = snapshot with { Digest = PricingSemanticHash.Compute(snapshot) };
                    if (!admissions.TryGet("GLBX.MDP3", out current) || current != admission) return;
                    if (publisher.Publish(snapshot, plan.ValueDate, universe.ConfigurationDigest, Interlocked.Increment(ref version), lookup))
                    { published.Add(lookup); publishedLookups.Add(lookup); lastFailures.Remove(lookup); }
                    else Report(lookup, "PublicationFencedOrCapacityExceeded");
                }
            }
        }
        foreach (var stale in scopes.Keys.Where(x => !wanted.Contains(x)).ToArray())
        {
            var old = scopes[stale]; scopes.Remove(stale);
            await discovery.ReleaseAsync(old.Lease, token).ConfigureAwait(false);
        }
    }

    static DateTimeOffset MinimumExpiry(CompositionMarketInstrument row)
    {
        var context = row.Pricing!;
        var until = context.ValidUntilUtc;
        foreach (var quote in new[] { row.Quote!, row.Underlying! })
        {
            var expiry = quote.EventAtUtc.AddMilliseconds(context.MaximumQuoteAgeMilliseconds);
            if (expiry < until) until = expiry;
            expiry = quote.ReceivedAtUtc.AddMilliseconds(context.MaximumQuoteAgeMilliseconds);
            if (expiry < until) until = expiry;
        }
        return until;
    }

    void Report(string scope, string reason)
    {
        if (lastFailures.GetValueOrDefault(scope) == reason) return;
        if (lastFailures.Count >= 512) lastFailures.Clear();
        lastFailures[scope] = reason;
        logger.LogInformation("Strategy option cache unavailable: ScopeId={ScopeId}, GenerationId={GenerationId}, Reason={Reason}", scope, generation, reason);
    }

    async Task ReleaseAllAsync(CancellationToken token)
    {
        var retired = scopes.Values.ToArray(); scopes.Clear();
        foreach (var scope in retired)
        {
            try { await discovery.ReleaseAsync(scope.Lease, token).ConfigureAwait(false); }
            catch (CompositionMarketSourceException ex) { Report(scope.Universe.ScopeId, ex.Code); }
        }
    }

    /// <summary>Publishes every 250 ms; all provider waits occur on this hosted background task.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250), clock);
        try
        {
            do
            {
                try { await MaintainAsync(stoppingToken).ConfigureAwait(false); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Strategy option cache maintenance failed; GenerationId={GenerationId}", generation);
                    await Task.Delay(TimeSpan.FromSeconds(5), clock, stoppingToken).ConfigureAwait(false);
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally
        {
            publisher.Fence(generation);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try { await ReleaseAllAsync(timeout.Token).ConfigureAwait(false); }
            catch (Exception ex) { logger.LogWarning(ex, "Strategy option cache lease release failed during shutdown"); }
        }
    }
}
