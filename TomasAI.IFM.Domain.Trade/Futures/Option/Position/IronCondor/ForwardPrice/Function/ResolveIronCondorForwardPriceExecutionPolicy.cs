using ForwardPriceSessionId = TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IronCondorTradePlanId;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position.IronCondor.ForwardPrice;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.ForwardPrice.Function.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.ForwardPrice.Function;
public static class ResolveIronCondorForwardPriceExecutionPolicy
{
    /// <summary>Bounds calculation and successful-event persistence by the original request deadline.</summary>
    /// <param name="command">The immutable request and initial UTC request time.</param>
    /// <param name="stage">The current framework execution stage.</param>
    /// <param name="context">Supplies the actor clock.</param>
    /// <returns>The existing Function completion policy with a five-second overall deadline.</returns>
    public static FunctionExecutionPolicy ResolveExecutionPolicy(this CalculateIronCondorForwardPriceCommand command, FunctionFailureStage stage, IIronCondorForwardPriceFunctionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new(context.TimeProvider, command.RequestedAtUtc.AddSeconds(5), FunctionCompletionMode.EventThenProjection);
    }
}
