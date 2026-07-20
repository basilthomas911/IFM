using System.Collections.Concurrent;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Provides a context for interacting with an actor in an event-driven system.  This context allows sending commands,
/// events, and queries to the actor,  as well as retrieving responses or results.
/// </summary>
/// <remarks>The <see cref="EventActorContext"/> is designed to facilitate communication with an actor  identified
/// by a unique <see cref="ActorMailboxId"/>. It provides methods for sending  asynchronous messages, including
/// commands, events, and queries, and supports both  one-way and request-response messaging patterns.</remarks>
/// <param name="supervisor">The actor supervisor that manages producers/consumers and actor lifecycle.</param>
/// <param name="actorId">The mailbox identifier for the target actor.</param>
public class EventActorContext(IActorSupervisor supervisor, ActorMailboxId actorId)
    : IEventActorContext
{
    readonly IActorSupervisor _supervisor = IsArgumentNull.Set(supervisor);
    readonly ActorMailboxId _actorId = IsArgumentNull.Set(actorId);
    readonly ConcurrentDictionary<ActorThreadId, ActorMessageInfo> _messageInfo = new();

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
    /// Sets the message information associated with the specified actor thread identifier.
    /// </summary>
    /// <param name="threadId">The identifier of the actor thread for which to set the message information.</param>
    /// <param name="info">The message information to associate with the specified actor thread identifier.</param>
    /// <returns>true if the message information was successfully set; otherwise, false.</returns>
    public bool SetMessageInfo(ActorThreadId threadId, ActorMessageInfo info)
    {
        _messageInfo[threadId] = info;
        return true;
    }

    /// <summary>
    /// Gets information about the most recent message processed by the specified actor thread, if available.
    /// </summary>
    /// <param name="threadId">The identifier of the actor thread for which to retrieve message information.</param>
    /// <returns>An <see cref="ActorMessageInfo"/> instance containing details about the most recent message processed by the
    /// specified thread, or <see langword="null"/> if no information is available for the given thread.</returns>
    public ActorMessageInfo? GetMessageInfo(ActorThreadId threadId)
    {
        return _messageInfo.TryGetValue(threadId, out var info) ? info : null;
    }

    /// <summary>
    /// Sends a query to the actor and awaits a typed response.
    /// </summary>
    /// <typeparam name="TResult">The expected result type returned from the query.</typeparam>
    /// <param name="query">The query to send to the actor.</param>
    /// <returns>A task that produces the query result.</returns>
    public async ValueTask<ServiceResult<TResult>> RequestAsync<TResult, TQuery>(TQuery query)
        where TQuery : class, IQuery<TResult>
        where TResult : class
        => await (_producer ??= _supervisor.GetProducer(_actorId))
            .RequestAsync<TResult, TQuery   >(query.Subject, query);

    /// <summary>
    /// Sends a command to the actor and awaits a service result containing the command id.
    /// </summary>
    /// <typeparam name="TEntityId">The type of the entity identifier associated with the command.</typeparam>
    /// <param name="command">The command to send.</param>
    /// <param name="entityId">The entity identifier used when routing the command (provided by caller).</param>
    /// <returns>A <see cref="ServiceResult{Guid}"/> indicating success or failure and containing the command id on success.</returns>
    public async ValueTask<ServiceResult<GuidResult>> RequestAsync<TCommand, TEntityId>(TCommand command)
        where TCommand : class, ICommand<TEntityId>
        where TEntityId : IActorEntityId
        => await (_producer ??= _supervisor.GetProducer(_actorId))
            .RequestAsync<TCommand, TEntityId, GuidResult>(command.Subject, command, command.EntityId);

    /// <summary>
    /// Sends an event to the actor via the configured producer.
    /// </summary>
    /// <typeparam name="TEvent">The type of the event being sent.</typeparam>
    /// <param name="@event">The event instance to send.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the send has been initiated.</returns>
    public async ValueTask SendAsync<TEvent, TEntityId>(TEvent @event)
        where TEvent : class, IEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        var actorId = @event.Subject.ActorId;
        await (_jsProducer ??= _supervisor.GetJSProducer(actorId)).SendAsync<TEvent, TEntityId>(@event.Subject, @event);
        await (_producer ??= _supervisor.GetProducer(actorId)).SendAsync<TEvent, TEntityId>(@event.Subject, @event);
    }

    /// <summary>
    /// Asynchronously sends a command to the specified entity using its unique identifier.
    /// </summary>
    /// <remarks>A producer is obtained from the supervisor if one does not already exist before sending the
    /// command. Exceptions may be thrown if the sending process fails; callers should handle such cases as
    /// appropriate.</remarks>
    /// <typeparam name="TCommand">Specifies the type of the command to send. Must implement the ICommand interface for the specified entity ID
    /// type.</typeparam>
    /// <typeparam name="TEntityId">Specifies the type of the entity identifier. Must implement the IActorEntityId interface.</typeparam>
    /// <param name="command">The command instance to send, containing the subject and any relevant data for processing.</param>
    /// <param name="entityId">The identifier of the entity to which the command is directed. Used to route the command appropriately.</param>
    /// <returns>A ValueTask that represents the asynchronous operation of sending the command.</returns>
    public async ValueTask SendAsync<TCommand, TEntityId>(TCommand command, TEntityId entityId)
        where TCommand : class, ICommand<TEntityId>
        where TEntityId : IActorEntityId
        => await (_producer ??= _supervisor.GetProducer(_actorId))
            .SendAsync(command.Subject, command, entityId);

    /// <summary>
    /// Adds an event router that facilitates message routing between two actor mailboxes.
    /// </summary>
    /// <remarks>This method establishes a connection for event routing between the specified mailboxes,
    /// enabling communication between actors.</remarks>
    /// <param name="fromActorTypeId">The identifier of the source actor type from which events are routed.</param>
    /// <param name="actorVerb">The verb associated with the actor for which the event router is being added.</param>
    public void AddEventRouter(ActorTypeId routeFrom, ActorMailboxId toMailboxId)
        => _supervisor.AddEventRouter(routeFrom, toMailboxId);

    /// <summary>
    /// Removes the event router that routes events from one mailbox to another.
    /// </summary>
    /// <remarks>This method is used to stop the routing of events between the specified mailboxes. Ensure
    /// that the mailboxes are valid and currently have an event router established.</remarks>
    /// <param name="fromActorTypeId">The identifier of the source actor type from which events are being routed.</param>
    /// <param name="toMailboxId">The identifier of the destination mailbox to which events are being routed.</param>
    public void RemoveEventRouter(ActorTypeId fromActorTypeId, ActorMailboxId toMailboxId) 
        => _supervisor.RemoveEventRouter(fromActorTypeId, toMailboxId);
}