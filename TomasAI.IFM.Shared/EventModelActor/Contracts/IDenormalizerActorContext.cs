using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Represents the context for a denormalizer actor, providing access to the actor's identifier, container instance, and
/// methods for managing messages and events.
/// </summary>
/// <remarks>This interface is designed to facilitate interaction with a denormalizer actor, including sending
/// events, associating message metadata, and retrieving message information.</remarks>
public interface IDenormalizerActorContext
{
    ActorMailboxId ActorId { get; }
    IContainerInstance Container { get; }

    ValueTask SendAsync<TEvent, TEntityId>(TEvent @event) 
        where TEvent : class, IEvent<TEntityId>
        where TEntityId : IActorEntityId;

    ValueTask<ServiceResult<TResult>> RequestAsync<TResult, TQuery>(TQuery query)
        where TQuery : class, IQuery<TResult>
        where TResult : class;

    bool SetMessageInfo(ActorThreadId threadId, ActorMessageInfo info);
    ActorMessageInfo? GetMessageInfo(ActorThreadId threadId);
}
