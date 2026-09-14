using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Function;
using TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.RiskManager.Function.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.RiskManager.Function;

public static class CompletePositionExitRisk
{
    public static FunctionResult<PositionExitRiskCompletedEvent, ExitPositionWorkflowFailedEvent>
        Complete(
            this FunctionEventContext<EvaluatePositionExitRiskCommand> input,
            TimeProvider clock) =>
        StrategyExitFunctionEventMapping.CompleteRisk(
            input, FuturesPositionExitRiskFunctionActor.ActorName, clock);
}
