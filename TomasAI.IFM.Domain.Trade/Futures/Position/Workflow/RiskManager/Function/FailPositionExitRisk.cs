using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Function;
using TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.RiskManager.Function.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.RiskManager.Function;

public static class FailPositionExitRisk
{

    extension(FunctionEventContext<EvaluatePositionExitRiskCommand> input)
    {
        public FunctionResult<PositionExitRiskCompletedEvent, ExitPositionWorkflowFailedEvent>
            Fail(
                TimeProvider clock) =>
            StrategyExitFunctionEventMapping.FailRisk(
                input, FuturesPositionExitRiskFunctionActor.ActorName, clock);
    }
}
