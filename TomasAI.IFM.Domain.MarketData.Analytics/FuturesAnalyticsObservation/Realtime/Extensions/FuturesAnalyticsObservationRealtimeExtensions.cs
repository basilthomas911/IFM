using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAnalyticsObservation.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAnalyticsObservation.Realtime.Projector;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAnalyticsObservation.Realtime.Extensions;

/// <summary>Exposes the observation actor's typed context as readonly extension properties.</summary>
public static class FuturesAnalyticsObservationRealtimeExtensions
{
    extension(IRealtimeActorContext<FuturesAnalyticsObservationRealtimeActor> context)
    {
        /// <summary>Gets the typed domain context.</summary>
        public IFuturesAnalyticsObservationRealtimeContext DomainContext =>
            context as IFuturesAnalyticsObservationRealtimeContext
            ?? throw new InvalidOperationException("The observation actor requires its typed context.");
        /// <summary>Gets the persistence-first projector.</summary>
        public FuturesAnalyticsObservationRealtimeProjector ObservationProjector => context.DomainContext.Projector;
        /// <summary>Gets the typed actor logger.</summary>
        public ILogger<FuturesAnalyticsObservationRealtimeActor> Logger => context.DomainContext.Logger;
    }
}
