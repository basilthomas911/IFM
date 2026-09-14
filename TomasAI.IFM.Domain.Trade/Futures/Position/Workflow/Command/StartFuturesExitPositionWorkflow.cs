using TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.Command.State;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Domain.Trade.Position.Workflow.Model;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.Command;

public static class StartFuturesExitPositionWorkflow
{
    public static ServiceResult<GuidResult> Execute(this StartFuturesExitPositionWorkflowCommand command,
        FuturesExitPositionWorkflowCommandState state) =>
        ExitPositionWorkflowStart.Apply(command, state, command.ExitPlan.Plan,
            TradeStrategyKind.FuturesOutright, command.ExitPlan.Id);
}
