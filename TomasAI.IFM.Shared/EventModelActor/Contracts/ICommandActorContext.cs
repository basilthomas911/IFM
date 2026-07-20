using System.Collections.Concurrent;
using System.Collections.Immutable;
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
    bool SetMessageInfo(ActorThreadId threadId, string verb, ActorMessageInfo info);
    ActorMessageInfo? GetMessageInfo(ActorThreadId threadId, string verb);
}
