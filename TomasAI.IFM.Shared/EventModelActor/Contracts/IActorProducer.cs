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

    ValueTask SendAsync<TCommand, TEntityId>(
        ActorSubject subject,
        TCommand command,
        TEntityId entityId,
        CancellationToken cancellationToken)
        where TCommand : class, ICommand<TEntityId>
        where TEntityId : IActorEntityId
    {
        cancellationToken.ThrowIfCancellationRequested();
        return SendAsync(subject, command, entityId);
    }

    ValueTask SendAsync<TEvent, TEntityId>(ActorSubject subject, TEvent @event) 
        where TEvent : class, IEvent<TEntityId>
        where TEntityId : IActorEntityId;

    ValueTask SendAsync<TEvent, TEntityId>(
        ActorSubject subject,
        TEvent @event,
        CancellationToken cancellationToken)
        where TEvent : class, IEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        cancellationToken.ThrowIfCancellationRequested();
        return SendAsync<TEvent, TEntityId>(subject, @event);
    }

    ValueTask<ServiceResult<TResult>> RequestAsync<TResult, TQuery>(ActorSubject subject, TQuery query)
        where TQuery : class, IQuery<TResult>
        where TResult : class;

    ValueTask<ServiceResult<TResult>> RequestAsync<TResult, TQuery>(
        ActorSubject subject,
        TQuery query,
        CancellationToken cancellationToken)
        where TQuery : class, IQuery<TResult>
        where TResult : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        return RequestAsync<TResult, TQuery>(subject, query);
    }

    ValueTask<ServiceResult<TResult>> RequestAsync<TCommand,TEntityId, TResult>(ActorSubject subject, TCommand command, TEntityId entityId)
        where TCommand: class, ICommand<TEntityId>
        where TEntityId : IActorEntityId
        where TResult : class;

    ValueTask<ServiceResult<TResult>> RequestAsync<TCommand, TEntityId, TResult>(
        ActorSubject subject,
        TCommand command,
        TEntityId entityId,
        CancellationToken cancellationToken)
        where TCommand : class, ICommand<TEntityId>
        where TEntityId : IActorEntityId
        where TResult : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        return RequestAsync<TCommand, TEntityId, TResult>(subject, command, entityId);
    }

    ValueTask StartAsync(ActorMailboxId mailboxId);
    ValueTask StartAsync(ActorMailboxId mailboxId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return StartAsync(mailboxId);
    }
    ValueTask StopAsync();
    ValueTask StopAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return StopAsync();
    }
    bool IsRunning { get; }
}

/// <summary>
/// Defines a contract for producing durable actor events through NATS JetStream.
/// </summary>
/// <remarks>Implementations of this interface should ensure thread safety when interacting with actors. The
/// methods provided allow for non-blocking operations, supporting a reactive programming model. The IsRunning property
/// indicates whether the actor producer is currently active.</remarks>
public interface IJSActorProducer
{
    ValueTask SendAsync<TEvent, TEntityId>(ActorSubject subject, TEvent @event)
        where TEvent : class, IEvent<TEntityId>
        where TEntityId : IActorEntityId;

    ValueTask SendAsync<TEvent, TEntityId>(ActorSubject subject, TEvent @event, CancellationToken cancellationToken)
        where TEvent : class, IEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        cancellationToken.ThrowIfCancellationRequested();
        return SendAsync<TEvent, TEntityId>(subject, @event);
    }

    ValueTask StartAsync(ActorMailboxId mailboxId);
    ValueTask StartAsync(ActorMailboxId mailboxId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return StartAsync(mailboxId);
    }
    ValueTask StopAsync();
    ValueTask StopAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return StopAsync();
    }
    bool IsRunning { get; }
}
