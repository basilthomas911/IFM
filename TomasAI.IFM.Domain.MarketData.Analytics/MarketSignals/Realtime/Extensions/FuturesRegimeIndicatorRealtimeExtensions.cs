using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketSignals.Realtime.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketSignals.Realtime.Extensions;

/// <summary>Exposes readonly services retained by the typed regime-indicator context.</summary>
public static class FuturesRegimeIndicatorRealtimeExtensions
{
    extension(IRealtimeActorContext<FuturesRegimeIndicatorRealtimeActor> context)
    {
        /// <summary>Gets the domain-specific typed context.</summary>
        public IFuturesRegimeIndicatorRealtimeContext DomainContext =>
            IsArgumentNull.Set(context as IFuturesRegimeIndicatorRealtimeContext, nameof(context))!;
        /// <summary>Gets the storage-first realtime projector.</summary>
        public IRealtimeProjector<FuturesRegimeIndicatorRealtimeActor> Projector => context.DomainContext.Projector;
        /// <summary>Gets the typed logger.</summary>
        public ILogger<FuturesRegimeIndicatorRealtimeActor> Logger => context.DomainContext.Logger;
    }
}
