using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.EventProjector.Contracts;

public interface IEventProjector
{
    public string ActorName { get; }
    public string ProjectorName { get; }
    public string DurableProcessQueueName { get; }
    public string DurableReplayQueueName { get; }

    /// <summary>
    /// Gets the source event types this projector can process. The event names are used to select
    /// recoverable entries from the event log.
    /// </summary>
    public IReadOnlyCollection<Type> ProjectedEventTypes { get; }

    /// <summary>
    /// Starts the projector's durable process and replay queue workers.
    /// </summary>
    /// <param name="context">The runtime context created for the command actor that owns the projector.</param>
    /// <param name="cancellationToken">A token that cancels projector startup and the workers started by it.</param>
    /// <returns>A task-like value that represents the asynchronous startup operation.</returns>
    ValueTask StartAsync(ICommandActorContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the projector's durable process and replay queue workers.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the wait to begin the stop operation.</param>
    /// <returns>A task-like value that represents the asynchronous shutdown operation.</returns>
    ValueTask StopAsync(CancellationToken cancellationToken = default);

    public ValueTask DomainEventsProjectionAsync(DomainEventCollection domainEvents);
    public ValueTask ProcessDomainEventAsync(IEvent domainEvent);

    /// <summary>
    /// Gets the database context for event sourcing operations.
    /// </summary>
    public IEventSourceActorDbContext DbEventSource { get; }

    /// <summary>
    /// Gets the durable replay queue used for event replay operations.
    /// </summary>
    public IDurableReplayQueue DurableReplayQueue { get; }

    public IBlackboardService BlackboardService { get; }

    /// <summary>
    /// Gets the runtime context of the command actor that started the projector.
    /// </summary>
    /// <exception cref="InvalidOperationException">The projector has not been started.</exception>
    public ICommandActorContext Context { get; }
    /// <summary>
    /// Gets the logger used for operational and diagnostic messages.
    /// </summary>
    public ILogger Logger { get; }

}

/// <summary>
/// Defines a contract for an event projector that processes domain events for a specific command actor type.
/// </summary>
/// <typeparam name="TActor"></typeparam>
public interface IEventProjector<TActor> :IEventProjector 
    where TActor : ICommandActor<TActor>
{
}
