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

/// <summary>Provides the typed runtime context used by <see cref="TickAggregationRealtimeActor"/>.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class TickAggregationRealtimeContext : EventActorContext, ITickAggregationRealtimeContext
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

