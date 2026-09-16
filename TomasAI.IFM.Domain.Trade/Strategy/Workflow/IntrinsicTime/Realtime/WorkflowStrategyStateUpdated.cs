using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Logging;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime;

/// <summary>Handles one committed strategy-workflow snapshot.</summary>
public static class WorkflowStrategyStateUpdated
{
    /// <summary>Records observability and dispatches only work authorized by the committed state.</summary>
    public static async ValueTask ExecuteAsync(
        this WorkflowStrategyStateUpdatedEvent snapshot,
        IntrinsicTimeStrategyWorkflowRealtimeActor actor,
        IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context)
    {
        var logger = IntrinsicTimeStrategyWorkflowRealtimeActor.RequireEventContext(context).Logger;
        IntrinsicTimeStrategyWorkflowLogging.RealtimeStateReceived(
            logger, snapshot.Id, snapshot.WorkflowId.ToString(), snapshot.EntityId.Format(),
            snapshot.WorkflowRevision, snapshot.State.CurrentStage.ToString(), snapshot.State.Status.ToString());

        if (snapshot.State.RiskExecution is not null && snapshot.State.TerminalAtUtc is not null)
            _ = RiskManager.Model.RiskLatency.RecordWorkflow(snapshot.State);

        if (snapshot.State.TerminalAtUtc is not null)
        {
            var stage = IntrinsicTimeStrategyWorkflowRealtimeActor.CurrentStageState(snapshot.State);
            IntrinsicTimeStrategyWorkflowLogging.TerminalResult(
                logger, snapshot.Id, snapshot.WorkflowId.ToString(), snapshot.EntityId.Format(),
                snapshot.WorkflowRevision, snapshot.State.CurrentStage.ToString(), snapshot.State.Status.ToString(),
                snapshot.State.Outcome.ToString(), stage.ContinuationDecision.ToString(), stage.ParameterSetId,
                stage.ParameterSetVersion,
                (snapshot.State.TerminalAtUtc.Value - snapshot.State.StartedAtUtc).TotalMilliseconds);
        }

        if (snapshot.State is { Status: WorkflowStrategyMachineStatus.Started })
            await IntrinsicTimeStrategyWorkflowRealtimeActor.DispatchCommittedStateAsync(context, snapshot).ConfigureAwait(false);
        else if (snapshot.State.CompositionHandoff is not null)
            await IntrinsicTimeStrategyWorkflowRealtimeActor.ReconcileStoppedSelectionAsync(context, snapshot).ConfigureAwait(false);
    }
}