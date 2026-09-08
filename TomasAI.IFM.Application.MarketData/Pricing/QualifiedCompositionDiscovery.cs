using System.Collections.Immutable;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Framework.MarketData.Contracts;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.OptionPricer.Black76;

namespace TomasAI.IFM.Application.MarketData.Pricing;

public sealed record CompositionDiscoveryRequest(Guid LeaseId, Guid GenerationId, DateOnly ValueDate,
    DateOnly MaturityDate, DateTimeOffset DeadlineUtc, IReadOnlyList<OptionDefinitionCandidate> Definitions,
    bool ScopeComplete, OptionPricingCalendar Calendar, TreasuryPublicationPolicy Publication,
    TreasuryRateConversionPolicy Conversion);

public sealed record CompositionDiscoveryResult(WorkerOptionChainRequest? Lease,
    ImmutableArray<OptionPricingFailure> Exclusions, bool CompleteEmpty, OptionPricingFailure? Failure);

public sealed record CompositionContextRefreshResult(WorkerOptionChainRequest? Lease, WorkerOptionChainResult Result);

/// <summary>
/// Application entry point from a complete definition scope to a worker-owned qualified chain.
/// Reference lookup and Treasury I/O run here, never inside a worker quote callback.
/// </summary>
public sealed class QualifiedCompositionDiscovery(EuropeanOptionUniverse universe,
    IOptionPricingContextProvider contexts, ICompositionMarketDataApi market, TimeProvider? time = null,
    ILogger<QualifiedCompositionDiscovery>? logger = null, ICompositionRoutePlanStore? routePlans = null,
    DatasetDesiredSubscriptionRegistry? desired = null) : IAsyncDisposable
{
    readonly TimeProvider clock = time ?? TimeProvider.System;
    readonly ConcurrentDictionary<Guid, RefreshRegistration> registrations = new();
    readonly CancellationTokenSource stopping = new();
    readonly object lifecycle = new();
    Task? refreshLoop;
    int disposed;

    public async Task<CompositionDiscoveryResult> AcquireAsync(CompositionDiscoveryRequest request, CancellationToken cancellationToken,
        bool refreshAutomatically = true)
    {
        ArgumentNullException.ThrowIfNull(request);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        var now = clock.GetUtcNow();
        if (request.LeaseId == Guid.Empty || request.GenerationId == Guid.Empty || request.ValueDate == default
            || request.DeadlineUtc.Offset != TimeSpan.Zero || request.DeadlineUtc <= now || request.Definitions is null)
            return Failed("InvalidDiscoveryRequest");
        var wait = request.DeadlineUtc - now;
        if (wait > TimeSpan.FromSeconds(10)) wait = TimeSpan.FromSeconds(10);
        using var timeout = new CancellationTokenSource(wait, clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            // Copy before awaiting any external operation, so a caller cannot change enumeration mid-discovery.
            if (request.Definitions.Count > 512) return Failed("SnapshotLimit");
            var definitions = request.Definitions.ToArray();
            var qualified = await universe.QualifyAsync(definitions, request.ScopeComplete, now, linked.Token).ConfigureAwait(false);
            if (qualified.Failure is not null) return new(null, qualified.Exclusions, false, qualified.Failure);
            if (qualified.Definitions.IsEmpty) return new(null, qualified.Exclusions, true, null);
            var options = ImmutableArray.CreateBuilder<WorkerOptionDefinition>(qualified.Definitions.Length);
            foreach (var option in qualified.Definitions)
            {
                var context = await contexts.PrepareAsync(option.Convention, request.Calendar, request.Publication,
                    request.Conversion, request.GenerationId, OptionCalculator.EngineVersion, clock.GetUtcNow(), linked.Token).ConfigureAwait(false);
                // A just-completed HTTP refresh cannot be admitted before its response; the next valuation may use it.
                if (context.Failure?.Code is "TreasuryUnavailable" or "TreasuryStale")
                    context = await contexts.PrepareAsync(option.Convention, request.Calendar, request.Publication,
                        request.Conversion, request.GenerationId, OptionCalculator.EngineVersion, clock.GetUtcNow(), linked.Token).ConfigureAwait(false);
                if (context.Failure is not null) return new(null, qualified.Exclusions, false, context.Failure);
                options.Add(new(context.Context!, option.Strike, option.IsCall));
            }
            var resolved = options.MoveToImmutable();
            var scopeId = PricingSemanticHash.Compute(new { request.ValueDate, request.MaturityDate,
                Contracts = WorkerOptionChainRuntime.PhysicalDigest(resolved) });
            if (routePlans is not null)
            {
                var ids = resolved.Select(x => x.Pricing.Contract.ContractId).ToHashSet(StringComparer.Ordinal);
                var routes = CompositionRoutePlan.ResolveNative(desired ?? throw new InvalidOperationException("Native route registry is unavailable."),
                    "GLBX.MDP3", request.ValueDate, resolved.Select(x => x.Pricing.Contract.UnderlyingContractId));
                var plan = new CompositionRoutePlan(1, "", "GLBX.MDP3", request.MaturityDate,
                    definitions.Where(x => ids.Contains(x.ContractId)).OrderBy(x => x.ContractId, StringComparer.Ordinal).ToImmutableArray(),
                    [], routes, request.Calendar, request.Publication, request.Conversion).Seal();
                await routePlans.SaveAsync(plan, linked.Token).ConfigureAwait(false);
                scopeId = plan.PlanId;
            }
            var expiry = request.DeadlineUtc < clock.GetUtcNow().AddSeconds(120) ? request.DeadlineUtc : clock.GetUtcNow().AddSeconds(120);
            var lease = new WorkerOptionChainRequest(scopeId, request.LeaseId, request.GenerationId, request.ValueDate,
                request.MaturityDate, expiry, resolved);
            var acquired = await market.AcquireAsync("GLBX.MDP3", lease, linked.Token).ConfigureAwait(false);
            while (acquired.Failure?.Code == "UnderlyingQuoteUnavailable")
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25), clock, linked.Token).ConfigureAwait(false);
                acquired = await market.AcquireAsync("GLBX.MDP3", lease, linked.Token).ConfigureAwait(false);
            }
            if (acquired.Failure is not null) return new(null, qualified.Exclusions, false, acquired.Failure);
            if (!acquired.Active) return Failed("ChainUnavailable");
            if (refreshAutomatically)
            {
                registrations[lease.LeaseId] = new(lease, request.Calendar, request.Publication, request.Conversion, clock.GetUtcNow());
                lock (lifecycle) refreshLoop ??= Task.Run(RefreshLoopAsync);
            }
            return new(lease, qualified.Exclusions, false, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return Failed("DiscoveryTimeout"); }
        catch (CompositionMarketSourceException ex) { return Failed(ex.Code); }
    }

    public Task<WorkerOptionChainResult> ReleaseAsync(WorkerOptionChainRequest lease, CancellationToken cancellationToken)
    {
        registrations.TryRemove(lease.LeaseId, out _);
        return market.ReleaseAsync("GLBX.MDP3", new(lease.ScopeId, lease.LeaseId, lease.GenerationId), cancellationToken);
    }

    /// <summary>
    /// Rebuilds all pricing contexts outside tick callbacks and atomically replaces the expected worker batch.
    /// Failure leaves the old contexts subject to their original expiry; it never extends stale evidence.
    /// </summary>
    public async Task<CompositionContextRefreshResult> RefreshAsync(WorkerOptionChainRequest lease,
        OptionPricingCalendar calendar, TreasuryPublicationPolicy publication, TreasuryRateConversionPolicy conversion,
        CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10), clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        var values = ImmutableArray.CreateBuilder<WorkerOptionDefinition>(lease.Options.Length);
        foreach (var option in lease.Options.OrderBy(x => x.Pricing.Contract.ContractId, StringComparer.Ordinal))
        {
            var prepared = await contexts.PrepareAsync(option.Pricing.Contract, calendar, publication, conversion,
                lease.GenerationId, OptionCalculator.EngineVersion, clock.GetUtcNow(), linked.Token).ConfigureAwait(false);
            if (prepared.Failure?.Code is "TreasuryUnavailable" or "TreasuryStale")
                prepared = await contexts.PrepareAsync(option.Pricing.Contract, calendar, publication, conversion,
                    lease.GenerationId, OptionCalculator.EngineVersion, clock.GetUtcNow(), linked.Token).ConfigureAwait(false);
            if (prepared.Failure is not null) return new(null, new(false, prepared.Failure));
            values.Add(option with { Pricing = prepared.Context! with
            {
                MaximumQuoteAgeMilliseconds = Math.Min(prepared.Context!.MaximumQuoteAgeMilliseconds, option.Pricing.MaximumQuoteAgeMilliseconds),
                MaximumQuoteSkewMilliseconds = Math.Min(prepared.Context.MaximumQuoteSkewMilliseconds, option.Pricing.MaximumQuoteSkewMilliseconds)
            }});
        }
        var refreshed = lease with
        {
            Options = values.MoveToImmutable(),
            ExpectedContextDigest = PricingSemanticHash.Compute(new { lease.MaturityDate,
                Options = lease.Options.OrderBy(x => x.Pricing.Contract.ContractId, StringComparer.Ordinal).ToImmutableArray() })
        };
        var result = await market.AcquireAsync("GLBX.MDP3", refreshed, linked.Token).ConfigureAwait(false);
        return new(result.Active && result.Failure is null ? refreshed with { ExpectedContextDigest = null } : null, result);
    }

    static CompositionDiscoveryResult Failed(string code) => new(null, [], false,
        new(code, "Discovery", "", "Complete qualified discovery inputs are unavailable."));

    async Task RefreshLoopAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1), clock);
        try
        {
            while (await timer.WaitForNextTickAsync(stopping.Token).ConfigureAwait(false))
                await RefreshRegisteredAsync(stopping.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
    }

    /// <summary>Refreshes registered pricing batches away from quote callbacks; rejected/failed refresh never extends old validity.</summary>
    public async Task RefreshRegisteredAsync(CancellationToken cancellationToken)
    {
        foreach (var (id, registration) in registrations.ToArray())
        {
            var now = clock.GetUtcNow();
            if (registration.Lease.LeaseExpiresAtUtc <= now) { registrations.TryRemove(id, out _); continue; }
            if (registration.CheckedAtUtc.AddSeconds(30) > now
                && registration.Lease.Options.All(x => x.Pricing.ValidUntilUtc > now.AddSeconds(2)
                    && Black76PricingModel.ValidateContext(x.Pricing, now) is null)) continue;
            try
            {
                var refreshed = await RefreshAsync(registration.Lease, registration.Calendar, registration.Publication,
                    registration.Conversion, cancellationToken).ConfigureAwait(false);
                registrations.TryUpdate(id, registration with
                { Lease = refreshed.Lease ?? registration.Lease, CheckedAtUtc = now }, registration);
                if (refreshed.Result.Failure is { } failure)
                    logger?.LogWarning("Composition context refresh rejected for scope {Scope}: {Reason}", registration.Lease.ScopeId, failure.Code);
            }
            catch (Exception error) when (error is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                registrations.TryUpdate(id, registration with { CheckedAtUtc = now }, registration);
                logger?.LogWarning("Composition context refresh failed for scope {Scope}: {ErrorType}", registration.Lease.ScopeId, error.GetType().Name);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        await stopping.CancelAsync().ConfigureAwait(false);
        Task? pending; lock (lifecycle) pending = refreshLoop;
        if (pending is not null) await pending.ConfigureAwait(false);
        registrations.Clear();
        stopping.Dispose();
    }

    sealed record RefreshRegistration(WorkerOptionChainRequest Lease, OptionPricingCalendar Calendar,
        TreasuryPublicationPolicy Publication, TreasuryRateConversionPolicy Conversion, DateTimeOffset CheckedAtUtc);
}
