using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Workflow.RiskManager.Function;

public static class ResolvePositionExitRiskExecutionPolicy
{
    public static FunctionExecutionPolicy ResolveExecutionPolicy(
        this EvaluatePositionExitRiskCommand command,
        FunctionFailureStage stage,
        TimeProvider clock) =>
        new(clock, null);
}
