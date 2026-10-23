using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;

namespace TomasAI.IFM.Application.MarketData.OptionChainCache;

/// <summary>Maintains independent strategy leases and prepared valuations. Consumers never call this service.</summary>
public sealed class OptionChainBackgroundUpdater(IOptionUniverseSource universes, QualifiedCompositionDiscovery discovery,
    ICompositionMarketDataApi market, DatasetWorkerAdmissionRegistry admissions, IOptionChainSnapshotPublisher publisher,
    ILogger<OptionChainBackgroundUpdater> logger, TimeProvider? time = null, OptionChainCoverageObservations? coverage = null, Recovery.OptionFeedProcessingHealth? processingHealth = null) : BackgroundService
{
    readonly TimeProvider clock = time ?? TimeProvider.System;
    readonly Dictionary<string, ActiveScope> scopes = new(StringComparer.Ordinal);
    sealed record ActiveScope(PreparedOptionUniverse Universe, WorkerOptionChainRequest Lease);
    readonly HashSet<string> publishedLookups = new(StringComparer.Ordinal);
    readonly SemaphoreSlim maintenance = new(1, 1);
    long version;
    Guid generation;
    readonly Dictionary<string, string> lastFailures = new(StringComparer.Ordinal);
    ImmutableArray<PreparedOptionUniverse> publishedPlans = [];
    DateTimeOffset nextPolicyRead;

    /// <summary>Stable scope identity pinned by the normalized reviewed plan, independent of worker generations.</summary>
    public static string ScopeId(CompositionMarketDataPlan plan, string horizon) => PricingSemanticHash.Compute(new { Plan = plan, Horizon = horizon });

    /// <summary>Runs one bounded maintenance cycle. This entry point also supports integration verification.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep native resource ownership and recovery failure ordering together during this storage compatibility refactor.")]
    public async Task MaintainAsync(CancellationToken token)
    {
        await maintenance.WaitAsync(token).ConfigureAwait(false);
        try { await MaintainCoreAsync(token).ConfigureAwait(false); }
        finally { maintenance.Release(); }
    }

    /// <summary>Confirms all background option-chain scopes are retired after dataset admission closes.</summary>
    public async Task CloseSessionAsync(CancellationToken token)
    {
        await maintenance.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (admissions.TryGet("GLBX.MDP3", out _))
                throw new InvalidOperationException("Option feeds must lose dataset admission before session cleanup.");
            publisher.Fence(generation);
            generation = Guid.Empty;
            publishedLookups.Clear(); lastFailures.Clear();
            publishedPlans = []; nextPolicyRead = default;
            await ReleaseAllAsync(token).ConfigureAwait(false);
        }
        finally { maintenance.Release(); }
    }

    async Task MaintainCoreAsync(CancellationToken token)
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
        var plans = CoalesceCoverage(publishedPlans);
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
            if (!string.Equals(plan.Dataset,"GLBX.MDP3",StringComparison.Ordinal) || !plan.IncludeOptions || !plan.ScopeComplete || plan.ValueDate != admission.ValueDate
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
                        StrategyOptionChainParameters = universe.Parameters, StrategyOptionChainBias = bias?.MarketBias.ToString(), StrategyOptionChainValueDate = bias is null ? null : plan.ValueDate };
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

    /// <summary>Batches overlapping policies for the same exact expiry and pricing context into one discovery scope.</summary>
    public static ImmutableArray<PreparedOptionUniverse> CoalesceCoverage(ImmutableArray<PreparedOptionUniverse> plans)
    {
        if (plans.IsDefault) return plans;
        var result = ImmutableArray.CreateBuilder<PreparedOptionUniverse>();
        foreach (var group in plans.GroupBy(x => PricingSemanticHash.Compute(new { Plan = x.MarketData with { Options = [] },
            Underlyings = x.MarketData.Options.Select(option => option.Definition.Underlying).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() })))
        {
            // Historical unparameterized lookups use the original plan digest as their public key.
            if (group.Any(x => x.Parameters is null)) { result.AddRange(group); continue; }
            var options = group.SelectMany(x => x.MarketData.Options).GroupBy(x => x.ContractId)
                .Select(x => x.Distinct().Count() == 1 ? x.First() : throw new InvalidDataException("Conflicting option definitions in background coverage batch."))
                .OrderBy(x => x.ContractId, StringComparer.Ordinal).ToImmutableArray();
            if (options.Length > 2048) throw new InvalidDataException("Batched option coverage exceeds the worker capacity.");
            foreach (var universe in group)
                result.Add(universe with { MarketData = universe.MarketData with { Options = options } });
        }
        return result.ToImmutable();
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
        if (string.Equals(lastFailures.GetValueOrDefault(scope),reason,StringComparison.Ordinal)) return;
        if (lastFailures.Count >= 512) lastFailures.Clear();
        lastFailures[scope] = reason;
        ApplicationDiagnosticLog.Write15(logger, null, scope, generation, reason);
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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "The owned background worker records or returns its operation failure while keeping subsequent independent work available.")]
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
                    if (admissions.TryGet("GLBX.MDP3", out var active)) processingHealth?.Report(active.ValueDate, active.GenerationId, "OptionChainMaintenance", ex, stoppingToken);
                    ApplicationDiagnosticLog.Write16(logger, ex, generation);
                    await Task.Delay(TimeSpan.FromSeconds(5), clock, stoppingToken).ConfigureAwait(false);
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { /* Cancellation or optional observation must not abort the enclosing shutdown/recovery boundary. */ }
        finally
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try { await maintenance.WaitAsync(timeout.Token).ConfigureAwait(false);
                try { publisher.Fence(generation); await ReleaseAllAsync(timeout.Token).ConfigureAwait(false); }
                finally { maintenance.Release(); } }
            catch (Exception ex) { ApplicationDiagnosticLog.Write17(logger, ex); }
        }
    }
    /// <summary>Releases maintenance synchronization after the hosted background task stops.</summary>
    public override void Dispose()
    {
        base.Dispose();
        maintenance.Dispose();
    }

}
