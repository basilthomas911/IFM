using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Represents the context for querying an actor, providing access to the actor's identifier and the ability to send
/// events to the actor asynchronously.
/// </summary>
/// <remarks>This interface is typically used to interact with an actor in a distributed or concurrent system. It
/// allows the caller to identify the actor and send events for processing.</remarks>
public interface IQueryActorContext
{
    ActorMailboxId ActorId { get; }
    IContainerInstance Container { get; }

    ValueTask SendAsync<TEvent, TEntityId>(TEvent @event)
        where TEvent : class, IEvent<TEntityId>
        where TEntityId : IActorEntityId;

    bool SetMessageInfo(ActorThreadId threadId, string verb, ActorMessageInfo info);
    ActorMessageInfo? GetMessageInfo(ActorThreadId threadId, string verb);

    ValueTask ReplyAsync<TResult>(ActorThreadId threadId, string verb, ServiceResult<TResult> replyResult);

}
