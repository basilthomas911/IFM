using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Domain.Trade.Position.Workflow.Function;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Workflow.RiskManager.Function.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Workflow.RiskManager.Function;

public static class CompletePositionExitRisk
{
    public static FunctionResult<PositionExitRiskCompletedEvent, ExitPositionWorkflowFailedEvent>
        Complete(
            this FunctionEventContext<EvaluatePositionExitRiskCommand> input,
            TimeProvider clock) =>
        StrategyExitFunctionEventMapping.CompleteRisk(
            input, VerticalSpreadPositionExitRiskFunctionActor.ActorName, clock);
}
