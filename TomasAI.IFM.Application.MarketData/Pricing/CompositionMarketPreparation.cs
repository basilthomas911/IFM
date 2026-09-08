using System.Collections.Immutable;
using System.Text.Json.Serialization;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Framework.MarketData.Contracts;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;

namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>
/// Reviewed bounded market universe embedded in the exact published composition policy. Option definitions
/// reference immutable ReferenceDb mappings; these are neither guessed from ES nor selected by workflow horizon.
/// </summary>
public sealed record CompositionMarketDataPlan
{
    [JsonRequired] public int SchemaVersion { get; init; } = 1;
    [JsonRequired] public string Dataset { get; init; } = "GLBX.MDP3";
    [JsonRequired] public string Root { get; init; } = "ES";
    [JsonRequired] public DateOnly ValueDate { get; init; }
    [JsonRequired] public bool IncludeOptions { get; init; }
    [JsonRequired] public bool ScopeComplete { get; init; }
    [JsonRequired] public DateOnly MaturityDate { get; init; }
    [JsonRequired] public ImmutableArray<OptionDefinitionCandidate> Options { get; init; } = [];
    [JsonRequired] public ImmutableArray<CompositionFutureDefinition> Futures { get; init; } = [];
    public OptionPricingCalendar? Calendar { get; init; }
    public TreasuryPublicationPolicy? Publication { get; init; }
    public TreasuryRateConversionPolicy? Conversion { get; init; }
}

/// <summary>Production workflow preparation path: resolve admission, qualify/allocate, await quotes and commit the first snapshot.</summary>
public sealed class CompositionMarketPreparation(QualifiedCompositionDiscovery discovery,
    CompositionPreparationService preparations, ICompositionPreparationStore store,
    DatasetWorkerAdmissionRegistry admissions, TimeProvider? time = null,
    ICompositionRoutePlanStore? routePlans = null, DatasetDesiredSubscriptionRegistry? desired = null)
{
    readonly TimeProvider clock = time ?? TimeProvider.System;

    public async Task<CompositionPreparationResult> PrepareAsync(CompositionPreparationKey key, CompositionMarketDataPlan plan,
        string horizon, DateTimeOffset deadline, CancellationToken cancellationToken)
    {
        CompositionPreparationService.ValidateKey(key);
        var saved = await store.ReadAsync(key, cancellationToken).ConfigureAwait(false);
        if (saved is not null)
        {
            CompositionPreparationService.Validate(saved);
            return saved.Snapshot.ValidUntilUtc > clock.GetUtcNow() ? new(saved, null) : Failed("AcceptedSnapshotExpired");
        }
        if (plan.SchemaVersion != 1 || plan.Dataset != "GLBX.MDP3" || plan.Root != "ES" || !plan.ScopeComplete
            || horizon is not ("Daily" or "Weekly" or "Monthly") || deadline <= clock.GetUtcNow()
            || plan.Options.IsDefault || plan.Futures.IsDefault || plan.Options.Length > 512 || plan.Futures.Length > 16)
            return Failed("CompositionUniverseUnqualified");
        if (!admissions.TryGet(plan.Dataset, out var admitted) || admitted.ValueDate != plan.ValueDate)
            return Failed("Recovering");
        var wait = deadline - clock.GetUtcNow();
        if (wait > TimeSpan.FromSeconds(10)) wait = TimeSpan.FromSeconds(10);
        if (wait <= TimeSpan.Zero) return Failed("PreparationTimeout");
        using var timeout = new CancellationTokenSource(wait, clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        WorkerOptionChainRequest? lease = null;
        var retainLease = false;
        try
        {
            string scope;
            if (plan.IncludeOptions)
            {
                if (plan.Calendar is null || plan.Publication is null || plan.Conversion is null || !plan.Futures.IsEmpty)
                    return Failed("PricingContextUnavailable");
                var result = await discovery.AcquireAsync(new(Guid.NewGuid(), admitted.GenerationId, plan.ValueDate,
                    plan.MaturityDate, deadline, plan.Options, true, plan.Calendar, plan.Publication, plan.Conversion), linked.Token).ConfigureAwait(false);
                if (result.Failure is not null) return new(null, result.Failure);
                lease = result.Lease;
                if (result.CompleteEmpty)
                {
                    if (!admissions.TryGet(plan.Dataset, out var current) || current != admitted) return Failed("Recovering");
                    return await preparations.PrepareEmptyAsync(key, PricingSemanticHash.Compute(plan.Options), horizon,
                        admitted.GenerationId, deadline, linked.Token).ConfigureAwait(false);
                }
                if (lease is null) return Failed("ChainUnavailable");
                scope = lease.ScopeId;
            }
            else
            {
                if (!plan.Options.IsEmpty || plan.Futures.IsEmpty || plan.Futures.Any(x => x.Root != plan.Root || x.Dataset != plan.Dataset))
                    return Failed("ContractMetadataUnavailable");
                scope = PricingSemanticHash.Compute(plan.Futures);
                if (routePlans is not null)
                {
                    var routes = CompositionRoutePlan.ResolveNative(desired ?? throw new InvalidOperationException("Native route registry is unavailable."),
                        plan.Dataset, plan.ValueDate, plan.Futures.Select(x => x.ContractId));
                    var reconstruction = new CompositionRoutePlan(1, "", plan.Dataset, default, [],
                        plan.Futures.OrderBy(x => x.ContractId, StringComparer.Ordinal).ToImmutableArray(), routes, null, null, null).Seal();
                    await routePlans.SaveAsync(reconstruction, linked.Token).ConfigureAwait(false);
                    scope = reconstruction.PlanId;
                }
            }
            var request = new CompositionSnapshotRequest(Guid.NewGuid(), scope, horizon, admitted.GenerationId,
                default, deadline, plan.IncludeOptions, Futures: plan.Futures);
            while (true)
            {
                linked.Token.ThrowIfCancellationRequested();
                if (!admissions.TryGet(plan.Dataset, out var current) || current != admitted) return Failed("Recovering");
                // Unknown commit outcomes retain the bounded discovery lease for reconciliation.
                retainLease = true;
                var prepared = await preparations.PrepareAsync(key, plan.Dataset, request, linked.Token,
                    lease is null ? null : new(lease.ScopeId, lease.LeaseId, lease.GenerationId)).ConfigureAwait(false);
                retainLease = prepared.Preparation is not null;
                if (prepared.Preparation is not null || prepared.Failure?.Code is not ("QuoteUnavailable" or "UnderlyingQuoteUnavailable"
                    or "StaleData" or "IncoherentQuotes")) return prepared;
                await Task.Delay(TimeSpan.FromMilliseconds(25), clock, linked.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return Failed("PreparationTimeout"); }
        finally
        {
            // The saved preparation carries this lease identity. Selected-leg handoff acquires business
            // ownership before releasing it. Failed preparation releases; unknown commit outcomes retain its bounded TTL.
            if (lease is not null && !retainLease) await discovery.ReleaseAsync(lease, CancellationToken.None).ConfigureAwait(false);
        }
    }

    static CompositionPreparationResult Failed(string code) => new(null,
        new(code, "CompositionPreparation", "", "Reviewed complete market evidence is unavailable."));
}
