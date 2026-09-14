using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Workflow.Command.State;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Domain.Trade.Position.Workflow.Model;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Workflow.Command;

public static class StartIronCondorExitPositionWorkflow
{
    public static ServiceResult<GuidResult> Execute(this StartIronCondorExitPositionWorkflowCommand command,
        IronCondorExitPositionWorkflowCommandState state) =>
        ExitPositionWorkflowStart.Apply(command, state, command.ExitPlan.Plan,
            TradeStrategyKind.IronCondor, command.ExitPlan.Id);
}
