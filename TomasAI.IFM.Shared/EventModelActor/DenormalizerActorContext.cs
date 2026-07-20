using QLNet;
using System.Collections.Concurrent;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Provides a context for managing operations related to a denormalizer actor, including message handling, event
/// sending, and thread-specific message information.
/// </summary>
/// <remarks>This context is used to interact with a denormalizer actor within the actor system. It provides
/// access to the actor's mailbox identifier, the container instance managed by the supervisor, and methods for sending
/// events and managing thread-specific message information.</remarks>
/// <param name="supervisor"></param>
/// <param name="actorId"></param>
public class DenormalizerActorContext(IActorSupervisor supervisor, ActorMailboxId actorId)
 : IDenormalizerActorContext
{
    readonly IActorSupervisor _supervisor = IsArgumentNull.Set(supervisor);
    readonly ActorMailboxId _actorId = IsArgumentNull.Set(actorId);
    IActorProducer? _producer;
    ConcurrentDictionary<ActorThreadId, ActorMessageInfo> _messageInfo = [];

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
        => await (_producer ??= _supervisor.GetProducer(_actorId)).SendAsync<TEvent, TEntityId>(@event.Subject, @event);

    /// <summary>
    /// Sends a query to the associated actor and retrieves the result asynchronously.
    /// </summary>
    /// <remarks>This method ensures that the query is sent to the appropriate actor, creating a producer
    /// instance if necessary. The caller is responsible for handling the returned <see cref="ServiceResult{TResult}"/>
    /// to determine the success or failure of the operation.</remarks>
    /// <typeparam name="TResult">The type of the result expected from the query. Must be a reference type.</typeparam>
    /// <param name="query">The query to be sent. The query must implement <see cref="IQuery{TResult}"/> and specify the expected result
    /// type.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation. The task result contains a <see
    /// cref="ServiceResult{TResult}"/> encapsulating the outcome of the query, including the result data or any error
    /// information.</returns>
    public async ValueTask<ServiceResult<TResult>> RequestAsync<TResult, TQuery>(TQuery query) where TResult : class where TQuery : class, IQuery<TResult>
        => await (_producer ??= _supervisor.GetProducer(_actorId)).RequestAsync<TResult, TQuery>(query.Subject, query);

    /// <summary>
    /// Associates the specified message information with the given thread identifier.
    /// </summary>
    /// <param name="threadId">The unique identifier of the thread to associate with the message information.</param>
    /// <param name="info">The message information to associate with the specified thread identifier.</param>
    /// <returns><see langword="true"/> if the message information was successfully added; otherwise, <see langword="false"/> if
    /// the thread identifier already exists.</returns>
    public bool SetMessageInfo(ActorThreadId threadId, ActorMessageInfo info)
    {
        _messageInfo.TryRemove(threadId, out _);
        return _messageInfo.TryAdd(threadId, info);
    }

    /// <summary>
    /// Retrieves the message information associated with the specified actor thread ID.
    /// </summary>
    /// <param name="threadId">The unique identifier of the actor thread whose message information is to be retrieved.</param>
    /// <returns>An <see cref="ActorMessageInfo"/> object containing the message information for the specified thread ID,  or
    /// <see langword="null"/> if no information is found for the given thread ID.</returns>
    public ActorMessageInfo? GetMessageInfo(ActorThreadId threadId)
    {
        _messageInfo.TryGetValue(threadId, out var info);
        return info;
    }

    
}
