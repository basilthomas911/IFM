using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;

namespace TomasAI.IFM.Application.MarketData.Subscriptions;

public interface IDurableCompositionReconciler
{
    Task<DurableRealization?> ReconcileOnceAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Replays committed ownership at startup and after generation/date changes. Reference refresh runs
/// outside quote callbacks; accepted workflow snapshots are never read or modified here.
/// </summary>
public sealed class DurableCompositionRuntime(IDurableSubscriptionIntentStore store,
    ICompositionRoutePlanStore plans, DurableSubscriptionDelivery delivery,
    DatasetWorkerAdmissionRegistry admissions, DatasetDesiredSubscriptionRegistry desired,
    DatasetWorkerProcessRecoveryService workers, QualifiedCompositionDiscovery discovery,
    ILogger<DurableCompositionRuntime> logger, TimeProvider? time = null, string authorityScope = "IFM") : BackgroundService, IDurableCompositionReconciler
{
    readonly string Scope = !string.IsNullOrWhiteSpace(authorityScope) && authorityScope.Length <= 128
        ? authorityScope : throw new ArgumentException("Bounded authority scope required.", nameof(authorityScope));
    const string Dataset = "GLBX.MDP3";
    readonly TimeProvider clock = time ?? TimeProvider.System;
    readonly SemaphoreSlim serial = new(1, 1);
    readonly Dictionary<string, Registration> active = new(StringComparer.Ordinal);
    readonly SemaphoreSlim wake = new(0, 1);
    int wakePending;
    volatile bool hasActiveLeases;
    MarketDataSubscriptionCoordinator? coordinator;
    Guid generation;
    DateOnly valueDate;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        admissions.Changed += Signal;
        Signal();
        string? lastFailure = null;
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var timeout = hasActiveLeases ? TimeSpan.FromSeconds(30) : Timeout.InfiniteTimeSpan;
                await wake.WaitAsync(timeout, stoppingToken).ConfigureAwait(false);
                Interlocked.Exchange(ref wakePending, 0);
                try
                {
                    await ReconcileOnceAsync(stoppingToken).ConfigureAwait(false);
                    lastFailure = null;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception error)
                {
                    var failure = $"{error.GetType().Name}: {error.Message}";
                    if (failure != lastFailure)
                        logger.LogWarning("Durable composition reconciliation retained pending intent: {Failure}", failure);
                    lastFailure = failure;
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally { admissions.Changed -= Signal; }
    }

    void Signal()
    {
        if (Interlocked.Exchange(ref wakePending, 1) == 0)
            wake.Release();
    }

    /// <summary>One bounded, serialized retry. Acknowledgement requires every route in the current committed union.</summary>
    public async Task<DurableRealization?> ReconcileOnceAsync(CancellationToken cancellationToken)
    {
        await serial.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!admissions.TryGet(Dataset, out var admitted))
            {
                hasActiveLeases = false;
                return null;
            }
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30), clock);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
            var token = linked.Token;
            if (coordinator is null || valueDate != admitted.ValueDate)
            {
                if (coordinator is not null) await coordinator.DisposeAsync().ConfigureAwait(false);
                coordinator = new(Scope, Dataset, admitted.ValueDate, timeProvider: clock);
                valueDate = admitted.ValueDate;
            }
            if (generation != admitted.GenerationId)
            {
                active.Clear(); // Native handles and reference contexts belong exclusively to the old generation.
                generation = admitted.GenerationId;
            }
            var result = await delivery.ReconcileAsync(coordinator, async (manifest, ct) =>
            {
                var committed = await store.ReadAsync(Scope, Dataset, ct).ConfigureAwait(false);
                var leases = committed.Authorities.SelectMany(x => x.Leases).ToArray();
                if (leases.Any(x => x.Ticker.PricingPlanId is null))
                    throw new InvalidDataException("Committed ownership lacks an exact reconstruction plan.");
                var ids = leases.Select(x => x.Ticker.PricingPlanId!).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
                if (ids.Length > 8) throw new InvalidDataException("Durable composition scope capacity exceeded.");
                var resolved = new Dictionary<string, CompositionRoutePlan>(StringComparer.Ordinal);
                foreach (var id in ids)
                {
                    var plan = await plans.ReadAsync(id, ct).ConfigureAwait(false)
                        ?? throw new InvalidDataException("Committed route plan is unavailable.");
                    plan.Validate();
                    foreach (var lease in leases.Where(x => x.Ticker.PricingPlanId == id))
                        if (lease.Ticker.AssetKind == Contracts.SubscriptionAssetKind.FuturesOption
                            ? !plan.Options.Any(x => x.ContractId == lease.Ticker.ContractId && x.Definition.Underlying == lease.Ticker.UnderlyingContractId)
                            : !plan.Futures.Any(x => x.ContractId == lease.Ticker.ContractId))
                            throw new InvalidDataException("Committed leg differs from the immutable route plan.");
                    resolved.Add(id, plan);
                }
                var native = resolved.Values.SelectMany(x => x.NativeFutures).Distinct().ToArray();
                var target = desired.SetDurable(Dataset, valueDate, native);
                if (target.Revision != admitted.ManifestRevision)
                {
                    await workers.ApplyDesiredManifestAsync(Dataset, ct).ConfigureAwait(false);
                    return new(manifest.Revision, generation, false); // Re-enter only with the newly admitted identity.
                }
                foreach (var (id, registration) in active.ToArray())
                    if (!resolved.ContainsKey(id))
                    {
                        var removed = await workers.ApplyBusinessOwnershipAsync(Scope, Dataset, registration.Lease, ct).ConfigureAwait(false);
                        if (removed.Failure is not null) throw new CompositionMarketSourceException(removed.Failure.Code);
                        active.Remove(id);
                    }
                foreach (var (id, plan) in resolved.Where(x => !x.Value.Options.IsEmpty))
                {
                    WorkerOptionChainRequest? bootstrap = null;
                    if (!active.TryGetValue(id, out var registration))
                    {
                        var acquired = await discovery.AcquireAsync(new(Guid.NewGuid(), generation, valueDate,
                            plan.MaturityDate, clock.GetUtcNow().AddSeconds(120), plan.Options, true,
                            plan.Calendar!, plan.Publication!, plan.Conversion!), ct, refreshAutomatically: false).ConfigureAwait(false);
                        bootstrap = acquired.Lease;
                        if (acquired.Failure is not null || bootstrap is null || bootstrap.ScopeId != id)
                            throw new CompositionMarketSourceException(acquired.Failure?.Code ?? "RoutePlanChanged");
                        registration = new(bootstrap, clock.GetUtcNow());
                        active[id] = registration; // Retain the bounded bootstrap on uncertain delivery.
                    }
                    var owned = await workers.ApplyBusinessOwnershipAsync(Scope, Dataset, registration.Lease, ct).ConfigureAwait(false);
                    if (owned.Failure is not null || !owned.Active)
                        throw new CompositionMarketSourceException(owned.Failure?.Code ?? "OwnershipNotReady");
                    var ownerLease = leases.First(x => x.Ticker.PricingPlanId == id).LeaseId;
                    var control = registration.Lease with { LeaseId = ownerLease, LeaseExpiresAtUtc = clock.GetUtcNow().AddSeconds(120) };
                    if (registration.CheckedAtUtc.AddSeconds(30) <= clock.GetUtcNow()
                        || control.Options.Any(x => x.Pricing.ValidUntilUtc <= clock.GetUtcNow().AddSeconds(2)))
                    {
                        var refreshed = await discovery.RefreshAsync(control, plan.Calendar!, plan.Publication!, plan.Conversion!, ct).ConfigureAwait(false);
                        if (refreshed.Lease is null) throw new CompositionMarketSourceException(refreshed.Result.Failure?.Code ?? "PricingRefreshFailed");
                        control = refreshed.Lease;
                        registration = registration with { CheckedAtUtc = clock.GetUtcNow() };
                    }
                    active[id] = registration with { Lease = control };
                    if (bootstrap is not null) await discovery.ReleaseAsync(bootstrap, ct).ConfigureAwait(false);
                }
                var latest = await store.ReadAsync(Scope, Dataset, ct).ConfigureAwait(false);
                return new(manifest.Revision, generation, latest.Revision == committed.Revision
                    && admissions.TryGet(Dataset, out var current) && current == admitted);
            }, token).ConfigureAwait(false);
            hasActiveLeases = active.Count > 0;
            return result;
        }
        finally { serial.Release(); }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        if (coordinator is not null) await coordinator.DisposeAsync().ConfigureAwait(false);
    }

    sealed record Registration(WorkerOptionChainRequest Lease, DateTimeOffset CheckedAtUtc);
}
