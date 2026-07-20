using Cassandra;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.Design.Serialization;
using System.Diagnostics.Tracing;
using System.Transactions;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.Storage.ScyllaDb.EventSourceDb;

/// <summary>
/// Base repository that loads and saves event-sourced actor state from an event store and posts domain events
/// to an event denormalizer.
/// </summary>
public abstract class BaseEventSourceActorRepository
{
    readonly IEventSourceActorStateFactory _stateFactory;
    readonly IEventSourceActorDbContext _dbEventSource;
    readonly IActorService _actorService;
    readonly ILogger _logger;
    string _serviceId;

    /// <summary>
    /// Initializes a new instance of the BaseEventSourceActorRepository class with the specified dependencies required
    /// for event source actor management.
    /// </summary>
    /// <remarks>All parameters are required and must not be null. An exception is thrown if any dependency is
    /// not provided.</remarks>
    /// <param name="stateFactory">The factory used to create instances of event source actor state. This parameter cannot be null.</param>
    /// <param name="dbEventSource">The database context used to access event source actor data. This parameter cannot be null.</param>
    /// <param name="actorService">The service responsible for managing actor instances. This parameter cannot be null.</param>
    /// <param name="logger">The logger used to record events and errors. This parameter cannot be null.</param>
    public BaseEventSourceActorRepository(
        IEventSourceActorStateFactory stateFactory,
        IEventSourceActorDbContext dbEventSource,
        IActorService actorService,
        ILogger logger)
    {
        _stateFactory = IsArgumentNull.Set(stateFactory);
        _dbEventSource = IsArgumentNull.Set(dbEventSource);
        _actorService = IsArgumentNull.Set(actorService);
        _logger = IsArgumentNull.Set(logger);
        _serviceId = $"{GetType().Name}";
    }

    /// <summary>
    /// Resolves the numeric event stream identifier for a given string stream ID.
    /// </summary>
    /// <param name="streamIdValue">The logical stream ID value.</param>
    /// <returns>The numeric event stream ID.</returns>
    /// <exception cref="StorageException">Thrown when the event stream ID cannot be resolved.</exception>
    protected async Task<long> GetStreamId(string streamIdValue)
    {
        try
        {
            return await _dbEventSource.GetEventStreamIdAsync(streamIdValue);
        }
        catch (Exception ex)
        {
            _logger.LogErrorEvent(_serviceId, ex, "GetEventStreamIdAsync failed");
            throw new StorageException($"{_serviceId}.GetEntityIdAsync failed", ex);
        }
    }

    /// <summary>
    /// Loads the current actor state by creating an empty state instance. Event replay can be performed by the
    /// caller if needed.
    /// </summary>
    /// <typeparam name="TState">Actor state type.</typeparam>
    /// <param name="command">Command whose stream ID is used to locate the event stream.</param>
    /// <returns>The state instance for the actor.</returns>
    /// <exception cref="StorageException">Thrown when loading the state fails.</exception>
    protected async Task<TState> LoadStateAsync<TState>(ICommand command)
       where TState :  IEventSourceActorState<TState>
    {
        try
        {
            // load event stream from event storage filtered by stream id...
            var streamId = await GetStreamId(command.StreamId);
            var state = (TState) _stateFactory.CreateState<TState>();
            await _dbEventSource.MapReduceActorEventStreamAsync<TState>(streamId, e => state.ReplayEvents(e.Select(o => o.ToDomainEvent())));
            return state;
        }
        catch (Exception ex)
        {
            _logger.LogErrorEvent(_serviceId, ex, "LoadStateAsync failed for {StateName}",  typeof(TState).Name);
            var errorMsg = $"{_serviceId}.LoadStateAsync failed for {typeof(TState).Name}";
            throw new StorageException(errorMsg, ex);
        }
    }

    /// <summary>
    /// Loads the current actor state from the last N events in the corresponding event stream.
    /// </summary>
    /// <typeparam name="TState">Actor state type.</typeparam>
    /// <typeparam name="TEvent">Event type used to filter the event stream.</typeparam>
    /// <param name="command">Command whose stream ID is used to locate the event stream.</param>
    /// <param name="lastNRange">The number of most recent events to replay.</param>
    /// <returns>The state instance reconstructed from the last N events.</returns>
    /// <exception cref="StorageException">Thrown when loading the state fails.</exception>
    protected async Task<TState> LoadStateAsync< TState, TEvent>(ICommand command, int lastNRange)
        where TState : IEventSourceActorState<TState> where TEvent : IEvent
    {
        try
        {
            // load event stream from event storage from last N range of events...
            var streamId = await GetStreamId(command.StreamId);
            var state = (TState)_stateFactory.CreateState<TState>();

            await _dbEventSource.MapReduceActorEventStreamAsync<TState, TEvent>(streamId, lastNRange, state.ReplayEvents);
            _logger.LogInformationEvent($"{GetType().Name}", "loading state: {StateName} for command: {CommandName} from event stream: {StreamId} with domain events in last: {LastNRange}",
                typeof(TState).Name, command.CommandName, streamId, lastNRange);
            return state;
        }
        catch (Exception ex)
        {
            _logger.LogErrorEvent(_serviceId, ex, "LoadStateAsync - lastNRange failed for {StateName}", typeof(TState).Name);
            var errorMsg = $"{_serviceId}.LoadStateAsync - lastNRange failed for {typeof(TState).Name}";
            throw new StorageException(errorMsg, ex);
        }
    }

    /// <summary>
    /// Creates and returns an empty state for the specified type without replaying events.
    /// </summary>
    /// <typeparam name="TState">Actor state type.</typeparam>
    /// <returns>An empty state instance.</returns>
    /// <exception cref="StorageException">Thrown when the state cannot be created.</exception>
    protected async Task<TState> LoadEmptyStateAsync<TState>()
        where TState : IEventSourceActorState<TState>
    {
        var stateName = typeof(TState).Name;
        try
        {
            // no domain events to load, so return empty state...
            _logger.LogInformationEvent(_serviceId, "loading empty bounded context: {StateName} with no domain events", stateName);
            return await Task.FromResult((TState)_stateFactory.CreateState<TState>());
        }
        catch (Exception ex)
        {
            _logger.LogErrorEvent(_serviceId, ex, "LoadEmptyStateAsync failed for {StateName}", stateName);
            var errorMsg = $"{_serviceId}.LoadEmptyStateAsync failed for {stateName}";
            throw new StorageException(errorMsg, ex);
        }
    }

    /// <summary>
    /// Loads the current actor state from the most recent snapshot event and subsequent events in the stream.
    /// </summary>
    /// <typeparam name="TState">Actor state type.</typeparam>
    /// <typeparam name="TSnapshotEvent">Snapshot event type used to locate the snapshot in the stream.</typeparam>
    /// <param name="command">Command whose stream ID is used to locate the event stream.</param>
    /// <returns>The state instance reconstructed from the snapshot and following events.</returns>
    /// <exception cref="StorageException">Thrown when loading the state from snapshot fails.</exception>
    protected async Task<TState> LoadStateFromSnapshotAsync<TState, TSnapshotEvent>(ICommand command)
         where TState : IEventSourceActorState<TState> where TSnapshotEvent : IEvent
    {
        var stateName = typeof(TState).Name;
        var snapshotEventName = typeof(TSnapshotEvent).Name;
        try
        {
            // load bounded context event stream from most recent snapshot in event storage...
            var streamId = await GetStreamId(command.StreamId);
            var state = (TState)_stateFactory.CreateState<TState>();
            state.Id = command.Subject.ThreadId;
            await _dbEventSource.MapReduceActorEventStreamAsync<TState, TSnapshotEvent>(streamId, state.ReplayEvents);

            _logger.LogInformationEvent(_serviceId, "loading state: {StateName} for command: {CommandName} from snapshot: {SnapshotEventName} in event stream: {StreamId}",
                    stateName, command.CommandName, snapshotEventName, streamId);
            return state;
        }
        catch (Exception ex)
        {
            _logger.LogErrorEvent(_serviceId, ex, "LoadStateFromSnapshot failed for {StateName} from snapshot {SnapshotEventName}", stateName, snapshotEventName);
            var errorMsg = $"{_serviceId}.LoadStateFromSnapshot failed for {stateName} from snapshot {snapshotEventName}";
            throw new StorageException(errorMsg, ex);
        }
    }

    /// <summary>
    /// Persists the specified state and denormalizes any associated domain events asynchronously within a transactional
    /// scope.
    /// </summary>
    /// <remarks>If the state contains new events, they are persisted and then denormalized as part of a
    /// single transaction. No action is taken if there are no new events to process.</remarks>
    /// <typeparam name="TState">The type of the event-sourced actor state to persist. Must implement IEventSourceActorState<TState>.</typeparam>
    /// <param name="state">The current state instance to be saved. Must not be null and should contain any new events to persist and
    /// denormalize.</param>
    /// <param name="command">The command that triggered the state changes and events. Provides context for event persistence and
    /// denormalization.</param>
    /// <returns>A task that represents the asynchronous save and denormalization operation.</returns>
    /// <exception cref="StorageException">Thrown if an error occurs while saving the state or denormalizing events.</exception>
    protected async Task SaveStateAndDenormalizeEventsAsync<TState>(ICommandActorContext context, TState state, ICommand command)
        where TState : IEventSourceActorState<TState>
    {
        var stateName = typeof(TState).Name;
        try
        {
            // check for any state change events...
            if (state!.Events.Count > 0)
            {
                var domainEvents = await _dbEventSource.SaveEventsAsync(command.StreamId, command.CommandId, state.Events, async (e) => await DenormalizeEventsAsync(context, e));
                _logger.LogInformationEvent(_serviceId, "saving state: {StateName} with {EventsCount} domain events from command: {CommandName} to event stream: {StreamId}", stateName, domainEvents.Count, command.CommandName, command.StreamId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogErrorEvent(_serviceId, ex, "SaveStateAndDenormalizeEventsAsync failed for {StateName}", stateName);
            var errorMsg = $"{_serviceId}.SaveStateAndDenormalizeEventsAsync failed for {stateName} ";
            throw new StorageException(errorMsg, ex);
        }
    }

    /// <summary>
    /// Asynchronously applies a collection of domain events to the specified actor state for denormalization purposes.
    /// </summary>
    /// <typeparam name="TState">The type of the actor state that implements IEventSourceActorState<TState>.</typeparam>
    /// <param name="context">The actor context used to send events and interact with the actor system.</param>
    /// <param name="domainEvents">A collection of domain events to denormalize and apply to the actor state.</param>
    /// <returns>A ValueTask that represents the asynchronous denormalization operation.</returns>
    protected abstract ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents);
    
    /// <summary>
    /// Processes a denormalization event and updates the read model by executing the specified denormalizer action.
    /// Optionally posts the event and emits completion or failure events based on the outcome.
    /// </summary>
    /// <remarks>If the denormalizer action completes successfully, a completion event is emitted. If an
    /// exception occurs during processing, a failure event is emitted with details of the error. This method is
    /// intended for use within actor-based denormalization workflows.</remarks>
    /// <typeparam name="TEvent">The type of the denormalization event to process. Must implement IEvent<TEntityId>.</typeparam>
    /// <typeparam name="TComplete">The type of the completion event to emit upon successful denormalization. Must implement
    /// ICompleteEvent<TEntityId>.</typeparam>
    /// <typeparam name="TFail">The type of the failure event to emit if denormalization fails. Must implement IErrorEvent<TEntityId>.</typeparam>
    /// <typeparam name="TEntityId">The type of the entity identifier associated with the events. Must implement IActorEntityId.</typeparam>
    /// <param name="context">The actor context used to send events and interact with the actor system.</param>
    /// <param name="dernomalizeEvent">The denormalization event to process. Cannot be null and must have a valid command identifier.</param>
    /// <param name="denormalizerAction">A delegate representing the denormalizer logic to execute for updating the read model.</param>
    /// <param name="postDenormalizeEvent">true to post the denormalization event before executing the denormalizer action; otherwise, false. The default
    /// is true.</param>
    /// <returns>A ValueTask that represents the asynchronous operation. The result is true when the operation completes.</returns>
    protected async ValueTask<bool> UpdateReadModelAsync<TEvent, TComplete, TFail, TEntityId>(ICommandActorContext context, TEvent dernomalizeEvent, Func<ValueTask> denormalizerAction, bool postDenormalizeEvent = true)
        where TEvent : class, IEvent<TEntityId>
        where TComplete : class, ICompleteEvent<TEntityId>
        where TFail : class, IErrorEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        try
        {
            dernomalizeEvent.CheckForEmptyCommandId();
            if (postDenormalizeEvent)
            {
                EventInitHelper.SetProperty(dernomalizeEvent, nameof(IEvent.Subject), new ActorSubject(ActorType.Event, dernomalizeEvent.Subject.Name
                    , dernomalizeEvent.Subject.Verb, dernomalizeEvent.EntityId.Format()));
                await context.SendAsync<TEvent, TEntityId>(dernomalizeEvent);
            }
            await denormalizerAction();
            var completedEvent = dernomalizeEvent.ToCompleteEvent<TComplete, TEntityId>() as TComplete;
            if (completedEvent is not null)
            {
                await context.SendAsync<TComplete, TEntityId>(completedEvent);
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogErrorEvent(_serviceId, ex, "UpdateReadModelAsync failed for event: {EventName}", typeof(TEvent).Name);
            var failedEvent = dernomalizeEvent.ToFailEvent<TFail, TEntityId>(ex) as TFail;
            if (failedEvent is not null)
            {
                await context.SendAsync<TFail, TEntityId>(failedEvent);
            }
            return false;
        }
    }

    /// <summary>
    /// only post event with no read model update
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    protected async ValueTask<bool> PostEventAsync<TEvent, TEntityId>(ICommandActorContext context, TEvent e)
        where TEvent : class, IEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        e.CheckForEmptyCommandId();
        EventInitHelper.SetProperty(e, nameof(IEvent.Subject), new ActorSubject(ActorType.Event, 
            e.Subject.Name.Replace("Denormalizer", "Event"), 
            e.Subject.Verb, 
            e.EntityId.Format()));
        await context.SendAsync<TEvent, TEntityId>(e);
        return true;
    }

   
}