using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Function;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Workflow.RiskManager.Function.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Workflow.RiskManager.Function;

public static class FailPositionExitRisk
{

    extension(FunctionEventContext<EvaluatePositionExitRiskCommand> input)
    {
        public FunctionResult<PositionExitRiskCompletedEvent, ExitPositionWorkflowFailedEvent>
            Fail(
                TimeProvider clock) =>
            StrategyExitFunctionEventMapping.FailRisk(
                input, VerticalSpreadPositionExitRiskFunctionActor.ActorName, clock);
    }
}
