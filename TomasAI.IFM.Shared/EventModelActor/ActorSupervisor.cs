using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NATS.Client.Core;
using QLNet;
using System.Collections.Concurrent;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Supervises actors and manages actor lifecycle related components such as thread pool,
/// producers, consumers and thread state registry.
/// </summary>
public class ActorSupervisor : IActorSupervisor
{
    readonly IActorThreadPool _threadPool;
    readonly ConcurrentDictionary<ActorMailboxId, IActorProducer> _producers;
    readonly ConcurrentDictionary<ActorMailboxId, IJSActorProducer> _jsProducers;
    readonly ConcurrentDictionary<ActorType, IActorConsumer> _consumers;
    readonly ConcurrentDictionary<ActorType, IJSActorConsumer> _jsConsumers;
    readonly ConcurrentDictionary<ActorThreadId, IActorState> _threadState;
    readonly ConcurrentDictionary<ActorMailboxId, IActor> _children;
    readonly ConcurrentDictionary<ActorTypeId, List<ActorMailboxId>> _eventRouters;
    readonly ILogger<ActorSupervisor> _logger;
    readonly IContainerInstance _container;
    readonly static string _serviceId = "ActorSupervisor";

    /// <summary>
    /// Initializes a new instance of <see cref="ActorSupervisor"/>.
    /// </summary>
    public ActorSupervisor(IContainerInstance container, ILogger<ActorSupervisor> logger)
    {
        _container = IsArgumentNull.Set(container);
        _logger = logger ?? NullLogger<ActorSupervisor>.Instance;
        _producers = new ConcurrentDictionary<ActorMailboxId, IActorProducer>();
        _jsProducers = new ConcurrentDictionary<ActorMailboxId, IJSActorProducer>();
        _consumers = new ConcurrentDictionary<ActorType, IActorConsumer>();
        _jsConsumers = new ConcurrentDictionary<ActorType, IJSActorConsumer>();
        _threadState = new ConcurrentDictionary<ActorThreadId, IActorState>();
        _children = new ConcurrentDictionary<ActorMailboxId, IActor>();
        _eventRouters = new ConcurrentDictionary<ActorTypeId, List<ActorMailboxId>>();

        // Initialize thread pool with one thread per logical processor.
        //var pool = new ActorThreadPool(this, _logger);
        var pool = new ActorThreadPoolV2(this, _logger);
        pool.Initialize(Environment.ProcessorCount * 2);
        _threadPool = pool;
    }

    /// <summary>
    /// Adds an actor to supervision.
    /// </summary>
    /// <param name="actor">Actor to add. Must not be null.</param>
    public void AddActor(IActor actor)
    {
        IsArgumentNull.Check(actor);   
        _children.TryAdd(actor.Id, actor);
    }

    /// <summary>
    /// Removes an actor from supervision.
    /// </summary>
    /// <param name="actor">Actor to remove. Must not be null.</param>
    public void RemoveActor(IActor actor)
    {
        IsArgumentNull.Check(actor);
        _children.TryRemove(actor.Id, out _);
    }

    /// <summary>
    /// Determines whether an actor with the specified mailbox id exists.
    /// </summary>
    /// <param name="mailboxId">The mailbox id to check.</param>
    /// <returns>True if the actor exists; otherwise false.</returns>
    public bool ActorExists(ActorMailboxId mailboxId)
        => _children.ContainsKey(mailboxId);

    /// <summary>
    /// Registers or updates the state for an actor thread.
    /// </summary>
    /// <remarks>
    /// If an entry for the given thread already exists, it will be replaced with the provided state.
    /// </remarks>
    /// <param name="state">The actor thread state to add or update. Cannot be null.</param>
    public void AddThreadState(IActorState state)
    {
        IsArgumentNull.Check(state);
        IsArgumentNull.Check(state.Id);
        _threadState.AddOrUpdate(state.Id, state,  (_, __) => state);
    }

    /// <summary>
    /// Removes the specified thread state from the collection.
    /// </summary>
    /// <remarks>This method removes the thread state identified by its unique <see cref="IActorState.Id"/>
    /// from the internal collection. If the specified state does not exist in the collection, no action is
    /// taken.</remarks>
    /// <param name="state">The thread state to remove. The <see cref="IActorState.Id"/> property must not be <see langword="null"/>.</param>
    public void RemoveThreadState(IActorState state)
    {
        IsArgumentNull.Check(state);
        IsArgumentNull.Check(state.Id);
        _threadState.TryRemove(state.Id, out _);
    }

    /// <summary>
    /// Determines whether a thread state entry exists for the specified thread identifier.
    /// </summary>
    /// <param name="threadId">The actor thread identifier to check. Cannot be null.</param>
    /// <returns><see langword="true"/> if a state exists for the specified thread; otherwise, <see langword="false"/>.</returns>
    public bool ThreadStateExists(ActorThreadId threadId)
    {
        IsArgumentNull.Check(threadId);
        return _threadState.ContainsKey(threadId);
    }

    /// <summary>
    /// Gets all thread state entries for the specified actor mailbox.
    /// </summary>
    /// <param name="actorId">The actor mailbox identifier. Cannot be null.</param>
    /// <returns>
    /// An array of <see cref="IActorState"/> associated with the actor's mailbox. Returns an empty array when none exist.
    /// </returns>
    public IActorState[] GetThreadStates(ActorMailboxId actorId)
    {
        IsArgumentNull.Check(actorId);
        List<IActorState> list = [];
        foreach (var kvp in _threadState)
        {
            if (kvp.Key.MailboxId.Equals(actorId))
                list.Add(kvp.Value);
        }
        return [.. list];
    }

    /// <summary>
    /// Creates a new mailbox for the specified actor.
    /// </summary>
    /// <param name="mailboxId">The unique identifier of the actor for which the mailbox is being created.</param>
    /// <returns>An instance of <see cref="IActorMailbox"/> representing the newly created mailbox.</returns>
    public IActorMailbox CreateMailbox(ActorMailboxId mailboxId)
        => new ActorMailbox(this, mailboxId);

    /// <summary>
    /// Retrieves an actor thread for the specified thread id from the internal thread pool.
    /// </summary>
    /// <param name="threadId">The thread identifier.</param>
    /// <returns>An <see cref="IActorThread"/> instance.</returns>
    public IActorThread GetThread(ActorThreadId threadId)
        => _threadPool.GetThread(threadId);

    /// <summary>
    /// Asynchronously retrieves an actor thread for the specified thread id from the internal thread pool.
    /// </summary>
    /// <param name="threadId">The thread identifier.</param>
    /// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask{IActorThread}"/> representing the asynchronous operation.</returns>
    public ValueTask<IActorThread> GetThreadAsync(ActorThreadId threadId, CancellationToken ct)
        => _threadPool.GetThreadAsync(threadId, ct);

   
    /// <summary>
    /// Starts all registered consumers asynchronously.
    /// </summary>
    /// <remarks>This method iterates through all registered consumers and invokes their asynchronous start
    /// operation. Each consumer is provided with the current instance and its associated key.</remarks>
    /// <returns></returns>
    public async ValueTask StartConsumersAsync()
    {
        foreach (var consumer in _consumers)
            await consumer.Value.StartAsync(this, consumer.Key).ConfigureAwait(false);
        foreach (var jsConsumer in _jsConsumers)
            await jsConsumer.Value.StartAsync(this, jsConsumer.Key).ConfigureAwait(false);
    }

    /// <summary>
    /// Stops all active consumers asynchronously.
    /// </summary>
    /// <remarks>This method iterates through all registered consumers and invokes their asynchronous stop
    /// operation.  Each consumer is stopped in sequence, and the method awaits the completion of each stop operation 
    /// before proceeding to the next.</remarks>
    /// <returns></returns>
    public async ValueTask StopConsumersAsync()
    {
        foreach (var consumer in _consumers)
            await consumer.Value.StopAsync().ConfigureAwait(false);
        foreach (var jsConsumer in _jsConsumers)
            await jsConsumer.Value.StopAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a new instance of a command actor context for the specified actor.
    /// </summary>
    /// <param name="actorId">The unique identifier of the actor for which the context is created.</param>
    /// <returns>An instance of <see cref="ICommandActorContext"/> representing the command context for the specified actor.</returns>
    public ICommandActorContext CreateCommandActorContext(ActorMailboxId actorId)
        => new CommandActorContext(this, actorId);

    /// <summary>
    /// Creates a new instance of an event actor context for the specified actor.
    /// </summary>
    /// <param name="actorId">The unique identifier of the actor for which the context is created.</param>
    /// <returns>An <see cref="IEventActorContext"/> instance representing the context for the specified actor.</returns>
    public IEventActorContext CreateEventActorContext(ActorMailboxId actorId)
       => new EventActorContext(this, actorId);

    /// <summary>
    /// Creates a new instance of a query actor context for the specified actor.
    /// </summary>
    /// <param name="actorId">The unique identifier of the actor for which the context is being created.</param>
    /// <returns>An <see cref="IQueryActorContext"/> instance representing the query context for the specified actor.</returns>
    public IQueryActorContext CreateQueryActorContext(ActorMailboxId actorId)
       => new QueryActorContext(this, actorId);

    /// <summary>
    /// Creates and returns a new instance of a denormalizer actor context for the specified actor.
    /// </summary>
    /// <param name="actorId">The unique identifier of the actor for which the context is being created.</param>
    /// <returns>An instance of <see cref="IDenormalizerActorContext"/> representing the context for the specified actor.</returns>
    public IDenormalizerActorContext CreateDenormalizerActorContext(ActorMailboxId actorId)
        => new DenormalizerActorContext(this, actorId);

    /// <summary>
    /// Starts the actor associated with the specified mailbox ID.
    /// </summary>
    /// <param name="mailboxId">The unique identifier of the actor's mailbox.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no actor is found for the specified <paramref name="mailboxId"/>.</exception>
    public async ValueTask StartAsync(ActorMailboxId mailboxId)
    {
        IsArgumentNull.Check(mailboxId);
        if (!_children.TryGetValue(mailboxId, out var actor))
            throw new InvalidOperationException($"Actor with mailbox id '{mailboxId}' not found.");
        await actor.StartAsync(this).ConfigureAwait(false);
    }

    /// <summary>
    /// Stops the actor associated with the specified mailbox identifier.
    /// </summary>
    /// <param name="mailboxId">The unique identifier of the actor's mailbox to stop. Cannot be <see langword="null"/>.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no actor is found with the specified <paramref name="mailboxId"/>.</exception>
    public async ValueTask StopAsync(ActorMailboxId mailboxId)
    {
        IsArgumentNull.Check(mailboxId);
        if (!_children.TryGetValue(mailboxId, out var actor))
            throw new InvalidOperationException($"Actor with mailbox id '{mailboxId}' not found.");
        await actor.StopAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Adds a producer to the specified mailbox.
    /// </summary>
    /// <param name="mailboxId">The unique identifier of the mailbox to associate with the producer.</param>
    /// <param name="producer">The producer to be added to the mailbox.</param>
    /// <exception cref="InvalidOperationException">Thrown if a producer for the specified <paramref name="mailboxId"/> already exists.</exception>
    public void AddProducer(ActorMailboxId mailboxId, IActorProducer producer)
    {
        IsArgumentNull.Check(mailboxId);
        if (!_producers.TryAdd(mailboxId, producer))
            throw new InvalidOperationException($"Producer for mailbox '{mailboxId}' already exists.");
    }

    /// <summary>
    /// Removes the producer associated with the specified mailbox identifier.
    /// </summary>
    /// <param name="mailboxId">The unique identifier of the mailbox whose producer is to be removed.</param>
    /// <exception cref="InvalidOperationException">Thrown if no producer is found for the specified <paramref name="mailboxId"/>.</exception>
    public void RemoveProducer(ActorMailboxId mailboxId)
    {
        IsArgumentNull.Check(mailboxId);
        if (!_producers.TryRemove(mailboxId, out _))
            throw new InvalidOperationException($"Producer for mailbox '{mailboxId}' not found.");
    }

    /// <summary>
    /// Gets the producer associated with the given mailbox id.
    /// </summary>
    /// <param name="mailboxId">The mailbox id of the producer to retrieve.</param>
    /// <returns>An <see cref="IActorProducer"/> instance.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if no producer is registered for the mailbox id.</exception>
    public IActorProducer GetProducer(ActorMailboxId mailboxId)
    {
        if (!_producers.TryGetValue(mailboxId, out var producer))
            throw new KeyNotFoundException($"Producer for mailbox '{mailboxId}' not found.");
        return producer;
    }

    /// <summary>
    /// Associates a JetStream actor producer with the specified mailbox.
    /// </summary>
    /// <remarks>Each mailbox can have only one associated JetStream actor producer. Attempting to add a
    /// producer to a mailbox that already has one will result in an exception.</remarks>
    /// <param name="mailboxId">The identifier of the mailbox to which the JetStream actor producer will be associated. This parameter cannot
    /// be null.</param>
    /// <param name="producer">The JetStream actor producer to add. This parameter cannot be null.</param>
    /// <exception cref="InvalidOperationException">Thrown if a JetStream actor producer is already associated with the specified mailbox.</exception>
    public void AddJSProducer(ActorMailboxId mailboxId, IJSActorProducer producer)
    {
        IsArgumentNull.Check(mailboxId);
        if (!_jsProducers.TryAdd(mailboxId, producer))
            throw new InvalidOperationException($"JetStreamProducer for mailbox '{mailboxId}' already exists.");
    }

    /// <summary>
    /// Removes the JetStream actor producer associated with the specified mailbox identifier.
    /// </summary>
    /// <param name="mailboxId">The unique identifier of the mailbox whose JetStream actor producer is to be removed.</param>
    /// <exception cref="InvalidOperationException">Thrown if no JetStream actor producer is found for the specified <paramref name="mailboxId"/>.</exception>
    public void RemoveJSProducer(ActorMailboxId mailboxId)
    {
        IsArgumentNull.Check(mailboxId);
        if (!_jsProducers.TryRemove(mailboxId, out _))
            throw new InvalidOperationException($"JetStreamProducer for mailbox '{mailboxId}' not found.");
    }

    /// <summary>
    /// Retrieves the JetStream actor producer associated with the specified mailbox identifier.
    /// </summary>
    /// <remarks>This method is useful for obtaining the producer needed to send messages to the specified
    /// actor mailbox.</remarks>
    /// <param name="mailboxId">The identifier of the actor mailbox for which the JetStream producer is requested.</param>
    /// <returns>An instance of <see cref="IJSActorProducer"/> that corresponds to the specified mailbox identifier.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if no JetStream producer is found for the specified <paramref name="mailboxId"/>.</exception>
    public IJSActorProducer GetJSProducer(ActorMailboxId mailboxId)
    {
        if (!_jsProducers.TryGetValue(mailboxId, out var producer))
            throw new KeyNotFoundException($"JetStreamProducer for mailbox '{mailboxId}' not found.");
        return producer;
    }

    /// <summary>
    /// Registers a consumer for the specified actor type.
    /// </summary>
    /// <param name="actorType">The type of actor for which the consumer is being registered.</param>
    /// <param name="consumer">The consumer instance to associate with the specified actor type.</param>
    /// <exception cref="InvalidOperationException">Thrown if a consumer for the specified <paramref name="actorType"/> has already been registered.</exception>
    public void AddConsumer(ActorType actorType , IActorConsumer consumer)
    {
        if (!_consumers.TryAdd(actorType, consumer))
            throw new InvalidOperationException($"Consumer for actor type '{actorType}' already exists.");
        _consumers.TryAdd(actorType, consumer);
    }

    /// <summary>
    /// Adds a JetStream consumer for the specified actor type.
    /// </summary>
    /// <remarks>This method ensures that only one consumer can be registered per actor type. Attempting to
    /// add a second consumer for the same actor type will result in an exception.</remarks>
    /// <param name="actorType">The actor type for which the consumer is being registered. Each actor type can have only one associated
    /// consumer.</param>
    /// <param name="consumer">The consumer instance that will handle messages for the specified actor type. Cannot be null.</param>
    /// <exception cref="InvalidOperationException">Thrown if a consumer for the specified actor type has already been added.</exception>
    public void AddConsumer(ActorType actorType, IJSActorConsumer consumer)
    {
        if (!_jsConsumers.TryAdd(actorType, consumer))
            throw new InvalidOperationException($"JetStream Consumer for actor type '{actorType}' already exists.");
        _jsConsumers.TryAdd(actorType, consumer);
    }

    /// <summary>
    /// Removes the consumer associated with the specified actor type from the internal collections.
    /// </summary>
    /// <remarks>If the specified actor type does not exist in the collections, no action is taken.</remarks>
    /// <param name="actorType">The type of actor whose consumer is to be removed. This parameter cannot be null.</param>
    public void RemoveConsumer(ActorType actorType)
    {
        IsArgumentNull.Check(actorType);
        _consumers.TryRemove(actorType, out _);
        _jsConsumers.TryRemove(actorType, out _);
    }

    /// <summary>
    /// Registers an event route from one actor type to a specific actor mailbox.
    /// </summary>
    /// <param name="fromActorTypeId">The source actor type identifier whose events should be routed. Cannot be <see langword="null"/>.</param>
    /// <param name="toMailboxId">The destination mailbox identifier to receive the routed events. Cannot be <see langword="null"/>.</param>
    public void AddEventRouter(ActorTypeId fromActorTypeId, ActorMailboxId toMailboxId)
    {
        IsArgumentNull.Check(fromActorTypeId);
        IsArgumentNull.Check(toMailboxId);
        _eventRouters.AddOrUpdate(fromActorTypeId,
            _ => [toMailboxId],
            (_, list) => { list.Add(toMailboxId); return list; });
    }

    /// <summary>
    /// Removes a specific event route from one actor type to a specific actor mailbox.
    /// </summary>
    /// <remarks>If the destination is the last route for the source actor type, the entire entry is removed.</remarks>
    /// <param name="fromActorTypeId">The source actor type identifier. Cannot be <see langword="null"/>.</param>
    /// <param name="toMailboxId">The destination mailbox identifier to remove. Cannot be <see langword="null"/>.</param>
    public void RemoveEventRouter(ActorTypeId fromActorTypeId, ActorMailboxId toMailboxId)
    {
        IsArgumentNull.Check(fromActorTypeId);
        IsArgumentNull.Check(toMailboxId);
        if (_eventRouters.TryGetValue(fromActorTypeId, out var routes))
        {
            routes.Remove(toMailboxId);
            if (routes.Count == 0)
                _eventRouters.TryRemove(fromActorTypeId, out _);
        }
    }

    
    public async ValueTask RouteEventToAsync(NatsMsg<byte[]> routedFromMsg)
    {
        try
        {
            var msgSubject = routedFromMsg.Subject.ToSubject();
            if (_eventRouters.TryGetValue(msgSubject.ActorTypeId, out var routedToMailboxIds))
            {
                var jsEventConsumer = _jsConsumers[msgSubject.ActorType];
                foreach (var e in routedToMailboxIds)
                {
                    var routedToSubject = new ActorSubject(e.ActorType, e.Name, msgSubject.Verb, msgSubject.EntityId);
                    await jsEventConsumer.RouteEventToAsync(routedToSubject, routedFromMsg);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogErrorEvent(_serviceId, ex, "Error routing event from '{Subject}' to registered mailboxes.", routedFromMsg.Subject.ToSubject());
        }
    }

    /// <summary>
    /// Returns the supervised child actors dictionary.
    /// </summary>
    public IReadOnlyDictionary<ActorMailboxId, IActor> Children
        => _children;

    /// <summary>
    /// Gets the dictionary representing the state of all actor threads.
    /// </summary>
    public IReadOnlyDictionary<ActorThreadId, IActorState> ThreadState
        => _threadState;

    /// <summary>
    /// Gets the container instance associated with the current context.
    /// </summary>
    public IContainerInstance Container => _container;

    /// <summary>
    /// Gets the actor thread pool managed by this supervisor.
    /// </summary>
    public IActorThreadPool ThreadPool => _threadPool;
}
