using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Workflow.Command.State;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Model;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Workflow.Command;

public static class StartVerticalSpreadExitPositionWorkflow
{

    /// <param name="command">
    /// The concrete exit intent.
    /// The originating exit command.
    /// </param>
    extension(StartVerticalSpreadExitPositionWorkflowCommand command)
    {
        /// <summary>Computes exact exit-plan ownership before applying the workflow start.</summary>
        ///
        /// <param name = "state">The authoritative exit workflow state.</param>
        /// <returns>The command acceptance or business rejection.</returns>
        public ServiceResult<GuidResult> Execute(VerticalSpreadExitPositionWorkflowCommandState state)
        {
            ArgumentNullException.ThrowIfNull(command);
            ArgumentNullException.ThrowIfNull(state);
            command.Compute(state, out var exitWorkflowChange);
            if (exitWorkflowChange.AlreadyStarted)
                return new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
            var errorMsg = "ExitPositionWorkflow.STATE.APPLY_FAILED: unable to apply start event";
            var updated = exitWorkflowChange switch
            {
                _ when exitWorkflowChange.RejectionReason is not null => command.UpdateFailed(ref errorMsg, exitWorkflowChange.RejectionReason),
                _ => state.Update(command.CreateExitPositionWorkflowStartedEvent(exitWorkflowChange), command)
            };
            return updated ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId)) : command.UpdateFailed(errorMsg);
        }

        /// <summary>Computes an immutable exit-workflow proposal without changing state.</summary>
        ///
        /// <param name = "state">The authoritative workflow owner.</param>
        /// <param name = "exitWorkflowChange">The proposed exit or business rejection.</param>
        /// <returns>True when the exit workflow is valid.</returns>
        internal bool Compute(VerticalSpreadExitPositionWorkflowCommandState state, out ExitPositionWorkflowChange exitWorkflowChange)
        {
            exitWorkflowChange = ExitPositionWorkflowStart.Compute(command, state.Started, command.ExitPlan.Plan, TradeStrategyKind.VerticalSpread, command.ExitPlan.Id);
            return exitWorkflowChange.RejectionReason is null;
        }

        /// <summary>Creates the workflow start with the originating command ID.</summary>
        ///
        /// <param name = "exitWorkflowChange">The accepted exit plan and its authority.</param>
        /// <returns>The event ready for state application.</returns>
        internal ExitPositionWorkflowStartedEvent CreateExitPositionWorkflowStartedEvent(ExitPositionWorkflowChange exitWorkflowChange) => new()
        {
            LifecycleVersion = 1,
            Id = Guid.NewGuid(),

            CommandId = command.CommandId,
            Subject = new(ActorType.Event, command.Subject.Name + "Event", ExitPositionWorkflowStartedEvent.Verb, command.EntityId.Format()),
            AggregateId = command.EntityId.Format(),
            EntityId = command.EntityId,
            ExitPlan = exitWorkflowChange.ExitPlan,
            StrategyKind = exitWorkflowChange.StrategyKind,
            SourcePlanEventId = exitWorkflowChange.SourcePlanEventId
        };
    }
}
