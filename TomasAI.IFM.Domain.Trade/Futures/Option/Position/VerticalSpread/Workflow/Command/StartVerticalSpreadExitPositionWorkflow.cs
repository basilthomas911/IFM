using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Workflow.Command.State;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Model;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Workflow.Command;

public static class StartVerticalSpreadExitPositionWorkflow
{
    public static ServiceResult<GuidResult> Execute(this StartVerticalSpreadExitPositionWorkflowCommand command,
        VerticalSpreadExitPositionWorkflowCommandState state) =>
        ExitPositionWorkflowStart.Apply(command, state, command.ExitPlan.Plan,
            TradeStrategyKind.VerticalSpread, command.ExitPlan.Id);
}
