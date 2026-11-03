using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Workflow.RiskManager.Function;

public static class ResolvePositionExitRiskExecutionPolicy
{

    extension(EvaluatePositionExitRiskCommand command)
    {
        public FunctionExecutionPolicy ResolveExecutionPolicy(
            FunctionFailureStage stage,
            TimeProvider clock) =>
            new(clock, null);
    }
}
