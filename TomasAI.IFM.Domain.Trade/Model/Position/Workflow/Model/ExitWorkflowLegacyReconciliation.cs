using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Persistence;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Model;

/// <summary>Repairs one historical exit stream from exact committed receipts, without starting or resubmitting work.</summary>
public static class ExitWorkflowLegacyReconciliation
{
    /// <summary>Advances one historical workflow stage when its required proof is already committed.</summary>
    /// <returns>Whether an existing receipt permitted a transition. A missing receipt leaves the stream unchanged.</returns>
    public static async Task<bool> ReconcileAsync(ExitPositionWorkflowId workflow, TradeStrategyKind kind,
        IPostgresEventTransaction transactions, IActorProducer producer, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(transactions);
        ArgumentNullException.ThrowIfNull(producer);
        if (!workflow.IsValid) throw new ArgumentException("A valid workflow identity is required.", nameof(workflow));
        var source = await transactions.ExecuteAsync((db, ct) =>
            ExitWorkflowReceiptStore.LatestOwnerSourceAsync(db, workflow, kind, ct), token).ConfigureAwait(false);
        return source is not null && await AdvanceAsync(source, transactions, producer, token).ConfigureAwait(false);
    }

    internal static bool IsHistorical(IEvent source) => source switch
    {
        ExitPositionWorkflowStartedEvent { LifecycleVersion: 0 } => true,
        ExitPositionWorkflowLifecycleEvent { Snapshot.Started.LifecycleVersion: 0 } => true,
        _ => false
    };

    internal static async Task<bool> AdvanceAsync(IEvent source, IPostgresEventTransaction transactions,
        IActorProducer producer, CancellationToken token)
    {
        if (!IsHistorical(source)) return false;
        switch (source)
        {
            case ExitPositionWorkflowStartedEvent started:
                var composition = await transactions.ExecuteAsync((db, ct) =>
                    ExitWorkflowReceiptStore.CompositionAsync(db, started, ct), token).ConfigureAwait(false);
                if (composition is null) return false;
                await ExitWorkflowContinuation.SendAsync(ExitWorkflowContinuation.ForSource(
                    new RecordExitOrderCompositionCommand { Receipt = composition },
                    RecordExitOrderCompositionCommand.Verb, started), producer, token).ConfigureAwait(false);
                return true;
            case ExitPositionWorkflowOrderComposedEvent composed:
                var risk = await transactions.ExecuteAsync((db, ct) =>
                    ExitWorkflowReceiptStore.RiskAsync(db, composed.Snapshot.Started, ct), token).ConfigureAwait(false);
                if (risk is null) return false;
                await ExitWorkflowContinuation.SendAsync(ExitWorkflowContinuation.ForSource(
                    new RecordExitRiskDecisionCommand { Receipt = risk },
                    RecordExitRiskDecisionCommand.Verb, composed), producer, token).ConfigureAwait(false);
                return true;
            case ExitPositionWorkflowRiskRecordedEvent { Snapshot.Projection.RiskDecision: { ExecuteTradeOrder: true } decision } accepted:
                var binding = await transactions.ExecuteAsync((db, ct) =>
                    ExitWorkflowReceiptStore.BindingAsync(db, decision, ct), token).ConfigureAwait(false);
                if (binding is null) return false;
                await ExitWorkflowContinuation.SendAsync(ExitWorkflowContinuation.ForSource(
                    new CompleteExitPositionWorkflowCommand { ExecutionAttemptId = binding.ExecutionAttemptId, BindingCommandId = binding.CommandId },
                    CompleteExitPositionWorkflowCommand.Verb, accepted), producer, token).ConfigureAwait(false);
                return true;
            default:
                return false;
        }
    }
}
