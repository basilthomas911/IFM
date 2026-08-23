using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Represents the context for an actor within the command processing system, providing methods to interact with the
/// actor, send messages, and manage its lifecycle.
/// </summary>
/// <remarks>This interface defines the contract for interacting with an actor, including sending events or
/// commands, checking the existence of actors or threads, and managing child actors. It also provides lifecycle methods
/// for starting and stopping the actor.</remarks>
public interface ICommandActorContext
{
    ActorMailboxId ActorId { get; }
    IContainerInstance Container { get; }

    ValueTask SendAsync<TEvent, TEntityId>(TEvent @event)
        where TEvent : class, IEvent<TEntityId>
        where TEntityId : IActorEntityId;

    ValueTask SendAsync<TEvent, TEntityId>(TEvent @event, CancellationToken cancellationToken)
        where TEvent : class, IEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        cancellationToken.ThrowIfCancellationRequested();
        return SendAsync<TEvent, TEntityId>(@event);
    }
    bool SetMessageInfo(ActorThreadId threadId, string verb, ActorMessageInfo info);
    ActorMessageInfo? GetMessageInfo(ActorThreadId threadId, string verb);
}

/// <summary>
/// Provides a command actor context that is associated with a specific actor type.
/// </summary>
/// <typeparam name="TActor">The command actor type associated with the context.</typeparam>
public interface ICommandActorContext<TActor> : ICommandActorContext
    where TActor : IActor
{
}
