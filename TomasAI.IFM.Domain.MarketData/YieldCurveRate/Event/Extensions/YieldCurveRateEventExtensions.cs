using TomasAI.IFM.Domain.MarketData.YieldCurveRate.Event.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.YieldCurveRate.Event.Extensions;

/// <summary>Exposes the yield-curve event context.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "C# extension members use their declared receiver; static conversion would remove the established actor context extension contract.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034", Justification = "C# 14 extension blocks generate nested implementation types; the public API consists of actor context extension members, not nested domain types.")]
public static class YieldCurveRateEventExtensions
{
    extension(IEventActorContext<YieldCurveRateEventActor> context)
    {
        /// <summary>Gets the domain context.</summary>
        public IYieldCurveRateEventContext YieldCurveRateContext => IsArgumentNull.Set(context as IYieldCurveRateEventContext)!;
    }
}
