using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketSignals.Realtime.Actor;

/// <summary>Defines readonly runtime services for the ordered regime-indicator actor.</summary>
public interface IFuturesRegimeIndicatorRealtimeContext
    : IRealtimeActorContext<FuturesRegimeIndicatorRealtimeActor>
{
    /// <summary>Gets the actor supervisor.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the storage-first realtime projector.</summary>
    IRealtimeProjector<FuturesRegimeIndicatorRealtimeActor> Projector { get; }
    /// <summary>Gets the typed actor logger.</summary>
    ILogger<FuturesRegimeIndicatorRealtimeActor> Logger { get; }
}

/// <summary>Provides the closed generic runtime context for the regime-indicator actor.</summary>
public sealed class FuturesRegimeIndicatorRealtimeContext
    : EventActorContext,
      IRealtimeActorContext<FuturesRegimeIndicatorRealtimeActor>,
      IFuturesRegimeIndicatorRealtimeContext
{
    /// <summary>Initializes the readonly runtime context.</summary>
    public FuturesRegimeIndicatorRealtimeContext(
        IActorSupervisor supervisor,
        IRealtimeProjector<FuturesRegimeIndicatorRealtimeActor> projector,
        ILogger<FuturesRegimeIndicatorRealtimeActor> logger)
        : base(supervisor, new(ActorType.Realtime, FuturesRegimeIndicatorRealtimeActor.ActorName))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        Projector = IsArgumentNull.Set(projector);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc />
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc />
    public IRealtimeProjector<FuturesRegimeIndicatorRealtimeActor> Projector { get; }
    /// <inheritdoc />
    public ILogger<FuturesRegimeIndicatorRealtimeActor> Logger { get; }
}
