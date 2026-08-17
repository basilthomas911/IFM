using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.EventProjector.Realtime.Contracts;

/// <summary>
/// Publishes and applies realtime projections once, without durable replay state.
/// </summary>
public interface IRealtimeProjector
{
    string ActorName { get; }
    string ProjectorName { get; }
    IReadOnlyCollection<Type> ProjectedEventTypes { get; }
    IReadOnlyCollection<RealtimeProjectionDescriptor> ProjectionDescriptors { get; }
    IEventActorContext Context { get; }
    ILogger Logger { get; }

    ValueTask StartAsync(
        IEventActorContext context,
        CancellationToken cancellationToken = default);

    ValueTask StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes the source event, applies its update once, and publishes either the
    /// corresponding complete or failure event.
    /// </summary>
    ValueTask<bool> ProcessRealtimeEventAsync(
        IEvent domainEvent,
        CancellationToken cancellationToken = default);
}

public interface IRealtimeProjector<TActor> : IRealtimeProjector
    where TActor : IEventActor<TActor>
{
}
