using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Subscriptions;
using TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
/// <summary>Durably retried acquire-before-release boundary; never recaptures or refreshes accepted evidence.</summary>
public sealed class CompositionDiscoveryHandoff(ICommittedBusinessEventJournal journal, IDurableSubscriptionIntentStore intent, ICompositionPreparationStore preparations, ICompositionMarketDataApi market, IDurableCompositionReconciler runtime)
{
    public async Task CompletePendingAsync(CancellationToken cancellationToken)
    {
        foreach (var row in await journal.ReadPendingHandoffsAsync(cancellationToken).ConfigureAwait(false))
        {
            if (row.ToDomainEvent()is not WorkflowStrategyStateUpdatedEvent workflow)
                throw new InvalidDataException("Committed workflow handoff cannot be decoded.");
            await CompleteAsync(workflow, row.EventVersion, row.StreamVersion > 0 ? row.StreamVersion : row.EventVersion, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task CompleteAsync(WorkflowStrategyStateUpdatedEvent workflow, long eventId, long sourceVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        var evidence = workflow.State.CompositionDispatch?.MarketEvidence;
        var selected = workflow.State.CompositionContracts;
        var terminal = workflow.State.Status is WorkflowStrategyMachineStatus.Failed or WorkflowStrategyMachineStatus.TimedOut or WorkflowStrategyMachineStatus.Cancelled || workflow.State.Status == WorkflowStrategyMachineStatus.Completed && workflow.State.Outcome == StrategyWorkflowOutcome.NoTrade;
        if (evidence is not null && (selected is not null || terminal))
        {
            var realized = await runtime.ReconcileOnceAsync(cancellationToken).ConfigureAwait(false);
            if (realized is not { AllRoutesReady: true })
                return;
            var current = await intent.ReadAsync("IFM", "GLBX.MDP3", cancellationToken).ConfigureAwait(false);
            var owner = current.Authorities.SingleOrDefault(x => string.Equals(x.SourceId, BusinessSubscriptionSourceKind.IntrinsicTimeWorkflow + ":" + workflow.WorkflowId, StringComparison.Ordinal));
            if (owner is null || owner.SourceVersion < sourceVersion)
                return;
            if (terminal && owner.Status != DurableAuthorityStatus.Terminal)
                return;
            if (!terminal && owner.Status != DurableAuthorityStatus.Terminal && !selected!.ContractIds.All(id => owner.Leases.Any(x => string.Equals(x.Ticker.PricingPlanId, selected.PricingPlanId, StringComparison.Ordinal) && string.Equals(x.Ticker.ContractId, id, StringComparison.Ordinal))))
                return;
            var prepared = await preparations.ReadAsync(new(evidence.WorkflowId, evidence.PreparationRevision, evidence.InputSha256), cancellationToken).ConfigureAwait(false) ?? throw new InvalidDataException("Accepted handoff preparation is unavailable.");
            CompositionPreparationService.Validate(prepared);
            if (!string.Equals(prepared.Digest, evidence.PreparationSha256, StringComparison.Ordinal) || prepared.Snapshot.SnapshotId != evidence.SnapshotId || selected is not null && (!string.Equals(prepared.Request.ScopeId, selected.PricingPlanId, StringComparison.Ordinal) || selected.ContractIds.Any(id => !prepared.Snapshot.Instruments.Any(x => string.Equals(x.Instrument.ContractId, id, StringComparison.Ordinal)))))
                throw new InvalidDataException("Handoff evidence differs from accepted preparation.");
            if (prepared.DiscoveryLease is { } discovery && discovery.GenerationId == realized.GenerationId)
            {
                var released = await market.ReleaseAsync(prepared.Dataset, discovery, cancellationToken).ConfigureAwait(false);
                if (released.Failure is not null)
                    throw new CompositionMarketSourceException(released.Failure.Code);
            }
        // A replacement worker has already destroyed all temporary leases of the preceding generation.
        }

        await journal.CompleteHandoffAsync(eventId, cancellationToken).ConfigureAwait(false);
    }
}
