using TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Function.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Function;

public static class ResolveFuturesTradePlanExecutionPolicy
{

    extension(UpdateFuturesTradePlanCommand command)
    {
        public FunctionExecutionPolicy ResolveExecutionPolicy(FunctionFailureStage stage, IFuturesTradePlanFunctionContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return new(context.TimeProvider, null, FunctionCompletionMode.EventThenProjection);
        }
    }
}
