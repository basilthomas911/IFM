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

    public ICommandActorContext Context { get;  }
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
