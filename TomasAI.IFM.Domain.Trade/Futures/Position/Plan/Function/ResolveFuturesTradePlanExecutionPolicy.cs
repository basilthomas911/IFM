using TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Function.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Function;

public static class ResolveFuturesTradePlanExecutionPolicy
{
    public static FunctionExecutionPolicy ResolveExecutionPolicy(this UpdateFuturesTradePlanCommand command,
        FunctionFailureStage stage, IFuturesTradePlanFunctionContext context) =>
        new(context.TimeProvider, null, FunctionCompletionMode.EventThenProjection);
}
