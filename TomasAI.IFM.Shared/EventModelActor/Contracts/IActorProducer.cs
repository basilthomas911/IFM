using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Defines an interface for producing and managing actor-based messaging operations,  including sending commands and
/// events, handling queries, and managing actor lifecycle states.
/// </summary>
/// <remarks>This interface provides methods for interacting with actor systems, enabling the sending of commands
/// and events,  querying actors, and controlling the lifecycle of actor mailboxes. Implementations of this interface
/// are expected  to handle the underlying messaging infrastructure and ensure proper delivery semantics.</remarks>
public interface IActorProducer
{
    ValueTask SendAsync<TCommand, TEntityId>(ActorSubject subject, TCommand command, TEntityId entityId)
        where TCommand : class, ICommand<TEntityId>
        where TEntityId : IActorEntityId;

    ValueTask SendAsync<TEvent, TEntityId>(ActorSubject subject, TEvent @event) 
        where TEvent : class, IEvent<TEntityId>
        where TEntityId : IActorEntityId;

    ValueTask<ServiceResult<TResult>> RequestAsync<TResult, TQuery>(ActorSubject subject, TQuery query)
        where TQuery : class, IQuery<TResult>
        where TResult : class;

    ValueTask<ServiceResult<TResult>> RequestAsync<TCommand,TEntityId, TResult>(ActorSubject subject, TCommand command, TEntityId entityId)
        where TCommand: class, ICommand<TEntityId>
        where TEntityId : IActorEntityId
        where TResult : class;

    ValueTask StartAsync(ActorMailboxId mailboxId);
    ValueTask StopAsync();
    bool IsRunning { get; }
}

/// <summary>
/// Defines a contract for producing NATS JetStream actors, enabling asynchronous communication by sending commands and
/// events to actor subjects and managing their lifecycle.
/// </summary>
/// <remarks>Implementations of this interface should ensure thread safety when interacting with actors. The
/// methods provided allow for non-blocking operations, supporting a reactive programming model. The IsRunning property
/// indicates whether the actor producer is currently active.</remarks>
public interface IJSActorProducer
{
    ValueTask SendAsync<TCommand, TEntityId>(ActorSubject subject, TCommand command, TEntityId entityId)
        where TCommand : class, ICommand<TEntityId>
        where TEntityId : IActorEntityId;

    ValueTask SendAsync<TEvent, TEntityId>(ActorSubject subject, TEvent @event)
        where TEvent : class, IEvent<TEntityId>
        where TEntityId : IActorEntityId;

    ValueTask StartAsync(ActorMailboxId mailboxId);
    ValueTask StopAsync();
    bool IsRunning { get; }
}
