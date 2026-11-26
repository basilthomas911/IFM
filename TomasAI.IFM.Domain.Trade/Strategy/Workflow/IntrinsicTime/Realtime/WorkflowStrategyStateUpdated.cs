using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Logging;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime;
/// <summary>Handles one committed strategy-workflow snapshot.</summary>
public static class WorkflowStrategyStateUpdated
{

    extension(WorkflowStrategyStateUpdatedEvent snapshot)
    {
        /// <summary>Records observability and dispatches only work authorized by the committed state.</summary>
        public async ValueTask ExecuteAsync(IntrinsicTimeStrategyWorkflowRealtimeActor actor, IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            if(snapshot.State.Status==WorkflowStrategyMachineStatus.Started && await WorkflowPublicationDelivery.RecoverAsync(
                IntrinsicTimeStrategyWorkflowRealtimeActor.RequireEventContext(context),snapshot.State).ConfigureAwait(false))return;
            if(snapshot.State.Status==WorkflowStrategyMachineStatus.Started && await WorkflowPublicationDelivery.InterruptOldSessionAsync(
                IntrinsicTimeStrategyWorkflowRealtimeActor.RequireEventContext(context),snapshot.State).ConfigureAwait(false))return;
            if (await IntrinsicTimeStrategyWorkflowRealtimeActor.RecoverInitializationAsync(context, snapshot).ConfigureAwait(false)) return;
            if (await IntrinsicTimeStrategyWorkflowRealtimeActor.RecoverCompositionFailureAsync(context, snapshot).ConfigureAwait(false)) return;
            if (await IntrinsicTimeStrategyWorkflowRealtimeActor.RecoverMarketConditionFailureAsync(context, snapshot).ConfigureAwait(false)) return;
            if (await IntrinsicTimeStrategyWorkflowRealtimeActor.RecoverReservationFailureAsync(context, snapshot).ConfigureAwait(false)) return;
            if(snapshot.State is {Status:WorkflowStrategyMachineStatus.Started,CurrentStage:StrategyWorkflowStage.OrderComposition,CompositionExecution:{} composition}
                && snapshot.State.EntityId==composition.WorkflowEntityId && snapshot.State.WorkflowId==composition.WorkflowId && snapshot.State.WorkflowRevision==composition.InputWorkflowRevision
                && await OrderComposer.Realtime.CompositionFunctionTerminalDelivery.RecoverAsync(
                IntrinsicTimeStrategyWorkflowRealtimeActor.RequireEventContext(context),composition).ConfigureAwait(false))return;
            if(snapshot.State is {Status:WorkflowStrategyMachineStatus.Started,CurrentStage:StrategyWorkflowStage.TradeSelection,SelectionDispatch:{} selection}
                && snapshot.State.EntityId==selection.WorkflowEntityId && snapshot.State.WorkflowId==selection.WorkflowId && snapshot.State.WorkflowRevision==selection.InputWorkflowRevision
                && snapshot.State.CompositionHandoff?.Status!=CompositionHandoffStatus.ReservationPending
                && await TradeSelection.Realtime.SelectionFunctionTerminalDelivery.RecoverAsync(
                    IntrinsicTimeStrategyWorkflowRealtimeActor.RequireEventContext(context),selection).ConfigureAwait(false))return;
            if(snapshot.State is {Status:WorkflowStrategyMachineStatus.Started,CurrentStage:StrategyWorkflowStage.MarketCondition}
                && snapshot.EntityId==snapshot.State.EntityId && snapshot.WorkflowId==snapshot.State.WorkflowId && snapshot.WorkflowRevision==snapshot.State.WorkflowRevision
                && await MarketCondition.Realtime.AssessmentFunctionTerminalDelivery.RecoverAsync(
                    IntrinsicTimeStrategyWorkflowRealtimeActor.RequireEventContext(context),snapshot).ConfigureAwait(false))return;
            if(snapshot.State is {Status:WorkflowStrategyMachineStatus.Started,CurrentStage:StrategyWorkflowStage.RegimeDiscovery}
                && snapshot.EntityId==snapshot.State.EntityId && snapshot.WorkflowId==snapshot.State.WorkflowId && snapshot.WorkflowRevision==snapshot.State.WorkflowRevision
                && await RegimeDiscovery.Realtime.RegimeFunctionTerminalDelivery.RecoverAsync(
                    IntrinsicTimeStrategyWorkflowRealtimeActor.RequireEventContext(context),snapshot).ConfigureAwait(false))return;
            var logger = IntrinsicTimeStrategyWorkflowRealtimeActor.RequireEventContext(context).Logger;
            if (logger.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Trace))
            {
                var loggingArgument1 = snapshot.WorkflowId.ToString();
                var loggingArgument2 = snapshot.EntityId.Format();
                var loggingArgument3 = snapshot.State.CurrentStage.ToString();
                var loggingArgument4 = snapshot.State.Status.ToString();
                IntrinsicTimeStrategyWorkflowLogging.RealtimeStateReceived(logger, snapshot.Id, loggingArgument1, loggingArgument2, snapshot.WorkflowRevision, loggingArgument3, loggingArgument4);
            }

            if (snapshot.State.RiskExecution is not null && snapshot.State.TerminalAtUtc is not null)
                _ = RiskManager.Model.RiskLatency.RecordWorkflow(snapshot.State);
            if (snapshot.State.TerminalAtUtc is not null)
            {
                var stage = IntrinsicTimeStrategyWorkflowRealtimeActor.CurrentStageState(snapshot.State);
                if (logger.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Information))
                {
                    var loggingArgument1 = snapshot.WorkflowId.ToString();
                    var loggingArgument2 = snapshot.EntityId.Format();
                    var loggingArgument3 = snapshot.State.CurrentStage.ToString();
                    var loggingArgument4 = snapshot.State.Status.ToString();
                    var loggingArgument5 = snapshot.State.Outcome.ToString();
                    var loggingArgument6 = stage.ContinuationDecision.ToString();
                    IntrinsicTimeStrategyWorkflowLogging.TerminalResult(logger, snapshot.Id, loggingArgument1, loggingArgument2, snapshot.WorkflowRevision, loggingArgument3, loggingArgument4, loggingArgument5, loggingArgument6, stage.ParameterSetId, stage.ParameterSetVersion, (snapshot.State.TerminalAtUtc.Value - snapshot.State.StartedAtUtc).TotalMilliseconds);
                }
            }

            if (snapshot.State is { Status: WorkflowStrategyMachineStatus.Started })
                await IntrinsicTimeStrategyWorkflowRealtimeActor.DispatchCommittedStateAsync(context, snapshot).ConfigureAwait(false);
            else if (snapshot.State.CompositionHandoff is not null)
                await IntrinsicTimeStrategyWorkflowRealtimeActor.ReconcileStoppedSelectionAsync(context, snapshot).ConfigureAwait(false);
        }
    }
}
