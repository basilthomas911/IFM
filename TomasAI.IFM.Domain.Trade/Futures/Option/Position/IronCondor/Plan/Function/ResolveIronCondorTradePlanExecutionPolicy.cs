using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function;

public static class ResolveIronCondorTradePlanExecutionPolicy
{

    extension(UpdateIronCondorTradePlanCommand command)
    {
        public FunctionExecutionPolicy ResolveExecutionPolicy(FunctionFailureStage stage, IIronCondorTradePlanFunctionContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return new(context.TimeProvider, null, FunctionCompletionMode.EventThenProjection);
        }
    }
}
