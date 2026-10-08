using TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.Command.State;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Model;
using TomasAI.IFM.Shared.EventSourcing;

using TomasAI.IFM.Shared.EventModelActor;
namespace TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.Command;

public static class StartFuturesExitPositionWorkflow
{
    /// <summary>Computes exact exit-plan ownership before applying the workflow start.</summary>
    /// <param name="command">The concrete exit intent.</param>
    /// <param name="state">The authoritative exit workflow state.</param>
    /// <returns>The command acceptance or business rejection.</returns>
    public static ServiceResult<GuidResult> Execute(this StartFuturesExitPositionWorkflowCommand command, FuturesExitPositionWorkflowCommandState state)
    {
        command.Compute(state, out var exitWorkflowChange);
        if (exitWorkflowChange.AlreadyStarted) return new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
        var errorMsg = "ExitPositionWorkflow.STATE.APPLY_FAILED: unable to apply start event";
        var updated = exitWorkflowChange switch
        {
            _ when exitWorkflowChange.RejectionReason is not null => command.UpdateFailed(ref errorMsg, exitWorkflowChange.RejectionReason),
            _ => state.Update(command.CreateExitPositionWorkflowStartedEvent(exitWorkflowChange), command)
        };
        return updated ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId)) : command.UpdateFailed(errorMsg);
    }
    /// <summary>Computes an immutable exit-workflow proposal without changing state.</summary>
    /// <param name="command">The concrete exit intent.</param>
    /// <param name="state">The authoritative workflow owner.</param>
    /// <param name="exitWorkflowChange">The proposed exit or business rejection.</param>
    /// <returns>True when the exit workflow is valid.</returns>
    internal static bool Compute(this StartFuturesExitPositionWorkflowCommand command, FuturesExitPositionWorkflowCommandState state, out ExitPositionWorkflowChange exitWorkflowChange)
    {
        exitWorkflowChange = ExitPositionWorkflowStart.Compute(command, state.Started, command.ExitPlan.Plan, TradeStrategyKind.FuturesOutright, command.ExitPlan.Id);
        return exitWorkflowChange.RejectionReason is null;
    }
    /// <summary>Creates the workflow start with the originating command ID.</summary>
    /// <param name="command">The originating exit command.</param>
    /// <param name="exitWorkflowChange">The accepted exit plan and its authority.</param>
    /// <returns>The event ready for state application.</returns>
    internal static ExitPositionWorkflowStartedEvent CreateExitPositionWorkflowStartedEvent(this StartFuturesExitPositionWorkflowCommand command, ExitPositionWorkflowChange exitWorkflowChange)
        => new()
        {
            CommandId = command.CommandId,
            EntityId = command.EntityId,
            ExitPlan = exitWorkflowChange.ExitPlan,
            StrategyKind = exitWorkflowChange.StrategyKind,
            SourcePlanEventId = exitWorkflowChange.SourcePlanEventId
        };
}
