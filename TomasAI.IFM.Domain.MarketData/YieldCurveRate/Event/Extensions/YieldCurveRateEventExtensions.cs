using TomasAI.IFM.Domain.MarketData.YieldCurveRate.Event.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.YieldCurveRate.Event.Extensions;

/// <summary>Exposes the yield-curve event context.</summary>
public static class YieldCurveRateEventExtensions
{
    extension(IEventActorContext<YieldCurveRateEventActor> context)
    {
        /// <summary>Gets the domain context.</summary>
        public IYieldCurveRateEventContext YieldCurveRateContext => IsArgumentNull.Set(context as IYieldCurveRateEventContext)!;
    }
}
