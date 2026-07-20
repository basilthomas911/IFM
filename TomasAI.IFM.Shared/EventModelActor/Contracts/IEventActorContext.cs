using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Represents the context for an event-driven actor, providing methods for sending messages,  querying state, and
/// managing actor lifecycle and relationships.
/// </summary>
/// <remarks>This interface defines the contract for interacting with an actor's context, including  sending
/// events and commands, querying state, checking actor existence, and managing child actors.  It is designed to
/// facilitate communication and lifecycle management in an actor-based system.</remarks>
public interface IEventActorContext
{
    ActorMailboxId ActorId { get; }
    IContainerInstance Container { get; }

    bool SetMessageInfo(ActorThreadId threadId, ActorMessageInfo info);
    ActorMessageInfo? GetMessageInfo(ActorThreadId threadId);

    ValueTask SendAsync<TEvent, TEntityId>(TEvent @event) 
        where TEvent : class, IEvent<TEntityId>
        where TEntityId : IActorEntityId;

    ValueTask SendAsync<TCommand, TEntityId>(TCommand command, TEntityId entityId)
        where TCommand : class, ICommand<TEntityId>
        where TEntityId : IActorEntityId;

    ValueTask<ServiceResult<TResult>> RequestAsync<TResult, TQuery>(TQuery query)
        where TQuery : class, IQuery<TResult>
        where TResult : class;

    ValueTask<ServiceResult<GuidResult>> RequestAsync<TCommand, TEntityId>(TCommand command)
        where TCommand : class, ICommand<TEntityId>
        where TEntityId : IActorEntityId;

    public void AddEventRouter(ActorTypeId fromActorTypeId, ActorMailboxId toMailboxId);
    public void RemoveEventRouter(ActorTypeId fromActorTypeId, ActorMailboxId toMailboxId);

}
