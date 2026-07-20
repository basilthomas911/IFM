using System.Collections.Concurrent;
using System.Collections.Immutable;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Reference.Commands;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Represents the context for an actor within the command processing system, providing access to the actor's mailbox,
/// container, and messaging operations.
/// </summary>
/// <remarks>This context is used to manage interactions with an actor, including sending events, associating
/// message information with threads, and retrieving message details. It is designed to work in conjunction with an <see
/// cref="IActorSupervisor"/> to ensure proper actor lifecycle management and message handling.</remarks>
/// <param name="supervisor"></param>
/// <param name="actorId"></param>
public class CommandActorContext (IActorSupervisor supervisor, ActorMailboxId actorId) 
    : ICommandActorContext
{
    readonly IActorSupervisor _supervisor = IsArgumentNull.Set(supervisor);
    readonly ActorMailboxId _actorId = IsArgumentNull.Set(actorId);
    ConcurrentDictionary<ActorThreadId, Dictionary<string,ActorMessageInfo>> _messageInfo = [];

    IActorProducer? _producer;
    IJSActorProducer? _jsProducer;

    /// <summary>
    /// Gets the mailbox identifier for the actor associated with this context.
    /// </summary>
    public ActorMailboxId ActorId 
        => _actorId;

    /// <summary>
    /// Gets the container instance managed by the supervisor.
    /// </summary>
    public IContainerInstance Container 
        => _supervisor.Container;


    /// <summary>
    /// Sends the specified event to the actor using the configured producer.
    /// </summary>
    /// <param name="@event">The event to send to the actor.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the send operation has been initiated.</returns>
    public async ValueTask SendAsync<TEvent, TEntityId>(TEvent @event)
        where TEvent : class, IEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        var actorId = @event.Subject.ActorId;
        await (_jsProducer ??= _supervisor.GetJSProducer(actorId)).SendAsync<TEvent, TEntityId>(@event.Subject, @event);
        await (_producer ??= _supervisor.GetProducer(actorId)).SendAsync<TEvent, TEntityId>(@event.Subject, @event);
    }

    /// <summary>
    /// Sets the message information associated with the specified verb for the given actor thread.
    /// </summary>
    /// <param name="threadId">The identifier of the actor thread for which to set the message information.</param>
    /// <param name="verb">The verb representing the message type to associate with the specified information. Cannot be null.</param>
    /// <param name="info">The message information to associate with the specified verb and actor thread. Cannot be null.</param>
    /// <returns>true if the message information was set successfully; otherwise, false.</returns>
    public bool SetMessageInfo(ActorThreadId threadId, string verb, ActorMessageInfo info)
    {
        if (!_messageInfo.ContainsKey(threadId))
            _messageInfo[threadId] = [];
        _messageInfo[threadId].Remove(verb);
        _messageInfo[threadId].Add(verb, info);
        return true;
    }

    /// <summary>
    /// Retrieves information about a message associated with the specified actor thread and verb, if available.
    /// </summary>
    /// <param name="threadId">The identifier of the actor thread for which to retrieve message information.</param>
    /// <param name="verb">The verb representing the type of message to look up. Cannot be null.</param>
    /// <returns>An <see cref="ActorMessageInfo"/> instance containing information about the message if found; otherwise, <see
    /// langword="null"/>.</returns>
    public ActorMessageInfo? GetMessageInfo(ActorThreadId threadId, string verb)
    {
        if (_messageInfo.TryGetValue(threadId, out Dictionary<string, ActorMessageInfo>? threadMap))
        {
            if (threadMap!.TryGetValue(verb, out ActorMessageInfo msgInfo))
                return msgInfo;
        }
        return default;
    }

}
