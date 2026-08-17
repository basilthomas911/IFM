using System.Collections.Concurrent;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

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
    readonly ConcurrentDictionary<(ActorThreadId ThreadId, string Verb), ActorMessageInfo> _messageInfo = [];

    readonly ActorEventPublisher _eventPublisher = new(supervisor, actorId);

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
        => await SendAsync<TEvent, TEntityId>(@event, CancellationToken.None).ConfigureAwait(false);

    public async ValueTask SendAsync<TEvent, TEntityId>(TEvent @event, CancellationToken cancellationToken)
        where TEvent : class, IEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        var started = ActorRuntimeMetrics.StartStage();
        try
        {
            await _eventPublisher.SendAsync<TEvent, TEntityId>(@event, cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            ActorRuntimeMetrics.RecordStageFailure(ActorRuntimeMetrics.PublicationStage, ActorType.Command);
            throw;
        }
        finally
        {
            ActorRuntimeMetrics.RecordStage(started, ActorRuntimeMetrics.PublicationStage, ActorType.Command);
        }
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
        _messageInfo[(threadId, verb)] = info;
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
        => _messageInfo.TryGetValue((threadId, verb), out var info) ? info : null;

}
