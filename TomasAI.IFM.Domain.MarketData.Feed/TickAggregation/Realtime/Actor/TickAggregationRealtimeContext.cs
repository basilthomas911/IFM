using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Realtime.Actor;

/// <summary>Defines the runtime services required by <see cref="TickAggregationRealtimeActor"/>.</summary>
public interface ITickAggregationRealtimeContext : IRealtimeActorContext<TickAggregationRealtimeActor>
{
    /// <summary>Gets the actor supervisor.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the actor logger.</summary>
    ILogger<TickAggregationRealtimeActor> Logger { get; }
    /// <summary>Gets the Projector service.</summary>
    IRealtimeProjector<TickAggregationRealtimeActor> Projector { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="TickAggregationRealtimeActor"/>.</summary>
public sealed class TickAggregationRealtimeContext : EventActorContext, IRealtimeActorContext<TickAggregationRealtimeActor>, ITickAggregationRealtimeContext
{
    /// <summary>Initializes the typed realtime context.</summary>
    public TickAggregationRealtimeContext(
        IActorSupervisor supervisor,
        ILogger<TickAggregationRealtimeActor> logger,
        IRealtimeProjector<TickAggregationRealtimeActor> projector)
        : base(supervisor, new ActorMailboxId(ActorType.Realtime, TickAggregationRealtimeActor.ActorName))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        Logger = IsArgumentNull.Set(logger);
        Projector = IsArgumentNull.Set(projector);
    }
    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public ILogger<TickAggregationRealtimeActor> Logger { get; }
    /// <inheritdoc/>
    public IRealtimeProjector<TickAggregationRealtimeActor> Projector { get; }
}

