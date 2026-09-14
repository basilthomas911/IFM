using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Function.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Function;

public static class ResolveVerticalSpreadTradePlanExecutionPolicy
{
    public static FunctionExecutionPolicy ResolveExecutionPolicy(this UpdateVerticalSpreadTradePlanCommand command,
        FunctionFailureStage stage, IVerticalSpreadTradePlanFunctionContext context) =>
        new(context.TimeProvider, null, FunctionCompletionMode.EventThenProjection);
}
