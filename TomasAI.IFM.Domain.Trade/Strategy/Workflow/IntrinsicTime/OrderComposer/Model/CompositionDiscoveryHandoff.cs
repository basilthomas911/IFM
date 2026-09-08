using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Subscriptions;
using TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;

/// <summary>Durably retried acquire-before-release boundary; never recaptures or refreshes accepted evidence.</summary>
public sealed class CompositionDiscoveryHandoff(ICommittedBusinessEventJournal journal,
    IDurableSubscriptionIntentStore intent, ICompositionPreparationStore preparations,
    ICompositionMarketDataApi market, IDurableCompositionReconciler runtime)
{
    public async Task CompletePendingAsync(CancellationToken cancellationToken)
    {
        foreach (var row in await journal.ReadPendingHandoffsAsync(cancellationToken).ConfigureAwait(false))
        {
            if (row.ToDomainEvent() is not WorkflowStrategyStateUpdatedEvent workflow)
                throw new InvalidDataException("Committed workflow handoff cannot be decoded.");
            var evidence = workflow.State.CompositionDispatch?.MarketEvidence;
            if (evidence is not null && workflow.State.CompositionContracts is { } selected)
            {
                var realized = await runtime.ReconcileOnceAsync(cancellationToken).ConfigureAwait(false);
                if (realized is not { AllRoutesReady: true }) return;
                var current = await intent.ReadAsync("IFM", "GLBX.MDP3", cancellationToken).ConfigureAwait(false);
                var owner = current.Authorities.SingleOrDefault(x => x.SourceId == BusinessSubscriptionSourceKind.IntrinsicTimeWorkflow + ":" + workflow.WorkflowId);
                if (owner is null || owner.SourceVersion < (row.StreamVersion > 0 ? row.StreamVersion : row.EventVersion)) return;
                if (owner.Status != DurableAuthorityStatus.Terminal
                    && !selected.ContractIds.All(id => owner.Leases.Any(x => x.Ticker.PricingPlanId == selected.PricingPlanId && x.Ticker.ContractId == id))) return;
                var prepared = await preparations.ReadAsync(new(evidence.WorkflowId, evidence.PreparationRevision, evidence.InputSha256), cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidDataException("Accepted handoff preparation is unavailable.");
                CompositionPreparationService.Validate(prepared);
                if (prepared.Digest != evidence.PreparationSha256 || prepared.Snapshot.SnapshotId != evidence.SnapshotId
                    || prepared.Request.ScopeId != selected.PricingPlanId
                    || selected.ContractIds.Any(id => !prepared.Snapshot.Instruments.Any(x => x.Instrument.ContractId == id)))
                    throw new InvalidDataException("Handoff evidence differs from accepted preparation.");
                if (prepared.DiscoveryLease is { } discovery && discovery.GenerationId == realized.GenerationId)
                {
                    var released = await market.ReleaseAsync(prepared.Dataset, discovery, cancellationToken).ConfigureAwait(false);
                    if (released.Failure is not null) throw new CompositionMarketSourceException(released.Failure.Code);
                }
                // A replacement worker has already destroyed all temporary leases of the preceding generation.
            }
            await journal.CompleteHandoffAsync(row.EventVersion, cancellationToken).ConfigureAwait(false);
        }
    }
}
