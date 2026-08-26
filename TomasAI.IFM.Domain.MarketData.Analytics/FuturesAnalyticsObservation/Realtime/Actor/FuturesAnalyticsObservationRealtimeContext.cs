using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAnalyticsObservation.Realtime.Projector;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAnalyticsObservation.Realtime.Actor;

/// <summary>Defines readonly services required by the shared observation realtime actor.</summary>
public interface IFuturesAnalyticsObservationRealtimeContext
    : IRealtimeActorContext<FuturesAnalyticsObservationRealtimeActor>
{
    /// <summary>Gets the actor supervisor.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the persistence-first realtime projector.</summary>
    FuturesAnalyticsObservationRealtimeProjector Projector { get; }
    /// <summary>Gets the futures market-session calendar.</summary>
    IMarketSessionCalendar Calendar { get; }
    /// <summary>Gets the contract/continuation series resolver.</summary>
    IFuturesAnalyticsSeriesResolver SeriesResolver { get; }
    /// <summary>Gets the server clock.</summary>
    TimeProvider TimeProvider { get; }
    /// <summary>Gets the typed logger.</summary>
    ILogger<FuturesAnalyticsObservationRealtimeActor> Logger { get; }
}

/// <summary>Provides the closed generic context used by the shared observation realtime actor.</summary>
public sealed class FuturesAnalyticsObservationRealtimeContext
    : EventActorContext,
      IRealtimeActorContext<FuturesAnalyticsObservationRealtimeActor>,
      IFuturesAnalyticsObservationRealtimeContext
{
    /// <summary>Initializes the readonly context.</summary>
    public FuturesAnalyticsObservationRealtimeContext(
        IActorSupervisor supervisor,
        FuturesAnalyticsObservationRealtimeProjector projector,
        IMarketSessionCalendar calendar,
        IFuturesAnalyticsSeriesResolver seriesResolver,
        TimeProvider timeProvider,
        ILogger<FuturesAnalyticsObservationRealtimeActor> logger)
        : base(supervisor, new(ActorType.Realtime, FuturesAnalyticsObservationRealtimeActor.ActorName))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        Projector = IsArgumentNull.Set(projector);
        Calendar = IsArgumentNull.Set(calendar);
        SeriesResolver = IsArgumentNull.Set(seriesResolver);
        TimeProvider = IsArgumentNull.Set(timeProvider);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc />
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc />
    public FuturesAnalyticsObservationRealtimeProjector Projector { get; }
    /// <inheritdoc />
    public IMarketSessionCalendar Calendar { get; }
    /// <inheritdoc />
    public IFuturesAnalyticsSeriesResolver SeriesResolver { get; }
    /// <inheritdoc />
    public TimeProvider TimeProvider { get; }
    /// <inheritdoc />
    public ILogger<FuturesAnalyticsObservationRealtimeActor> Logger { get; }
}
