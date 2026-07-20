using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Exceptions;

namespace TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;

/// <summary>
/// Provides an abstract base class for managing bounded contexts and event streams in an event-sourced system.
/// </summary>
/// <remarks>This class serves as a foundational repository for working with bounded contexts and event streams.
/// It provides methods for loading, saving, and processing bounded contexts, as well as handling domain events and
/// snapshots. Derived classes can extend this functionality to implement specific repository behaviors.</remarks>
/// <param name="boundedContextFactory"></param>
/// <param name="dbEventSource"></param>
/// <param name="eventDenormalizer"></param>
/// <param name="logger"></param>
public abstract class BaseEventSourceRepository(
    IBoundedContextFactory boundedContextFactory,
    IEventSourceDbContext dbEventSource,
    IEventDenormalizer eventDenormalizer,
    ILogger logger)
{
    readonly IBoundedContextFactory _boundedContextFactory = IsArgumentNull.Set(boundedContextFactory);
    readonly IEventSourceDbContext _dbEventSource = IsArgumentNull.Set(dbEventSource);
    readonly IEventDenormalizer _eventDenormalizer = IsArgumentNull.Set(eventDenormalizer);
    readonly ILogger _logger = IsArgumentNull.Set(logger);

    /// <summary>
    /// return entity id from entity id string value
    /// </summary>
    /// <param name="entityIdValue"></param>
    /// <returns></returns>
    protected async Task<long> GetStreamId(string streamIdValue)
    {
        try
        {
            return await _dbEventSource.GetEventStreamIdAsync(streamIdValue);
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "{RepoName}.GetEventStreamIdAsync failed", GetType().Name);
            throw new StorageException($"{GetType().Name}.GetEntityIdAsync failed", ex);
        }
    }

    /// <summary>
    /// Asynchronously loads a bounded context from the event stream based on the specified command.
    /// </summary>
    /// <typeparam name="TBoundedContext">The type of the bounded context to load.</typeparam>
    /// <typeparam name="TState">The type of the state associated with the bounded context.</typeparam>
    /// <param name="command">The command containing the stream identifier used to load the event stream.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the loaded bounded context.</returns>
    /// <exception cref="StorageException">Thrown if there is an error loading the bounded context from the event stream.</exception>
    protected async Task<IBoundedContext<TState>> LoadBoundedContextAsync<TBoundedContext, TState>(ICommand command) 
        where TBoundedContext : IBoundedContext<TState> where TState : IBoundedContextState<TState>
    {
        try
        {
            // load event stream from event storage filtered by stream id...
            var streamId = await GetStreamId(command.StreamId);
            var boundCtx = boundedContextFactory.CreateBoundedContext<TState>();
            await _dbEventSource.MapReduceEventStreamAsync<TBoundedContext>(streamId, e => boundCtx?.State.ReplayEvents(e.Select(o => o.ToDomainEvent())));
            return boundCtx;
        }
        catch (Exception ex)
        {
            var errorMsg = $"{GetType().Name}.LoadAggregateAsync failed for aggregate {typeof(TState).Name}";
            _logger.LogError(ex, "{RepoName}.LoadAggregateAsync failed for aggregate {StateName}", GetType().Name, typeof(TState).Name);
            throw new StorageException(errorMsg, ex);
        }
    }

    /// <summary>
    /// Asynchronously loads a bounded context by replaying a specified range of recent events.
    /// </summary>
    /// <typeparam name="TBoundedContext">The type of the bounded context to load.</typeparam>
    /// <typeparam name="TState">The type of the state associated with the bounded context.</typeparam>
    /// <typeparam name="TEvent">The type of events to replay in the bounded context.</typeparam>
    /// <param name="command">The command containing the stream identifier for the event stream.</param>
    /// <param name="lastNRange">The number of recent events to replay in the bounded context.</param>
    /// <returns>A task representing the asynchronous operation, with a result of the loaded bounded context.</returns>
    /// <exception cref="StorageException">Thrown if loading the bounded context fails due to storage issues.</exception>
    protected async Task<IBoundedContext<TState>> LoadBoundedContextAsync<TBoundedContext, TState, TEvent>(ICommand command, int lastNRange) 
        where TBoundedContext : IBoundedContext<TState> where TState : IBoundedContextState<TState> where TEvent : IEvent
    {
        try
        {
            // load event stream from event storage from last N range of events...
            var streamId = await GetStreamId(command.StreamId);
            var boundCtx = boundedContextFactory.CreateBoundedContext<TState>();
            await _dbEventSource.MapReduceEventStreamAsync<TBoundedContext,TEvent>(streamId, lastNRange, e => boundCtx?.State.ReplayEvents(e));
            _logger.LogInformationEvent($"{GetType().Name}", "loading bounded context: {StateName} for command: {CommandName} from event stream: {StreamId} with domain events in last: {LastNRange}",
                typeof(TState).Name, command.CommandName, streamId,  lastNRange);
            return boundCtx;
        }
        catch (Exception ex)
        {
            var errorMsg = $"{GetType().Name}.LoadBoundedContextAsync - lastNRange failed for bounded context {typeof(TState).Name}";
            _logger.LogError(ex, "{RepoName}.LoadBoundedContextAsync - lastNRange failed for bounded context {StateName}", GetType().Name, typeof(TState).Name);
            throw new StorageException(errorMsg, ex);
        }
    }

    /// <summary>
    /// Loads an empty bounded context of the specified type with no domain events.
    /// </summary>
    /// <typeparam name="TBoundedContext">The type of the bounded context to load.</typeparam>
    /// <typeparam name="TState">The type of the state associated with the bounded context.</typeparam>
    /// <returns>An instance of <see cref="IBoundedContext{TState}"/> initialized with no domain events.</returns>
    /// <exception cref="StorageException">Thrown if an error occurs while loading the bounded context.</exception>
    protected async Task<IBoundedContext<TState>> LoadEmptyBoundedContextAsync<TBoundedContext, TState>() 
        where TBoundedContext : IBoundedContext<TState> where TState : IBoundedContextState<TState>
    {
        try
        {
            // no domain events to load, so return empty bounded context...
            _logger.LogInformationEvent($"{GetType().Name}", "loading empty bounded context: {StateName} with no domain events",  typeof(TState).Name);
            return await Task.FromResult(_boundedContextFactory.CreateBoundedContext<TState>());
        }
        catch (Exception ex)
        {
            var errorMsg = $"{GetType().Name}.LoadEmptyBoundedContextAsync failed for {typeof(TBoundedContext).Name} {typeof(TState).Name}";
            _logger.LogError(ex, "{RepoName}.LoadEmptyBoundedContextAsync failed for {AggregateName} {StateName}", GetType().Name, typeof(TBoundedContext).Name, typeof(TState).Name);
            throw new StorageException(errorMsg, ex);
        }
    }

    /// <summary>
    /// load bounded context from event storage snapshot event
    /// </summary>
    /// <param name="command"></param>
    /// <returns></returns>
    protected async Task<IBoundedContext<TState>> LoadBoundedContextFromSnapshotAsync<TBoundedContext, TState, TSnapshotEvent>(ICommand command)
        where TBoundedContext : IBoundedContext<TState> where TState : IBoundedContextState<TState> where TSnapshotEvent : IEvent
    {
        try
        {
            // load bounded context event stream from most recent snapshot in event storage...
            var streamId = await GetStreamId(command.StreamId);
            var boundCtx = boundedContextFactory.CreateBoundedContext<TState>();
            await _dbEventSource.MapReduceEventStreamAsync<TBoundedContext, TSnapshotEvent>(streamId,  e => boundCtx?.State.ReplayEvents(e));
            _logger.LogInformationEvent($"{GetType().Name}", "loading bounded context: {StateName} for command: {CommandName} from snapshot: {SnapshotEventName} in event stream: {StreamId}",
                    typeof(TState).Name, command.CommandName, typeof(TSnapshotEvent).Name, streamId);
            return boundCtx;
        }
        catch (Exception ex)
        {
            var errorMsg = $"{GetType().Name}.LoadBoundedContextFromSnapshot failed for bounded context {typeof(TState).Name} from snapshot {typeof(TSnapshotEvent).Name}";
            _logger.LogError(ex, "{RepoName}.LoadBoundedContextFromSnapshot failed for bounded context {StateName} from snapshot {SnapshotEventName}", GetType().Name, typeof(TState).Name, typeof(TSnapshotEvent).Name);
            throw new StorageException(errorMsg, ex);
        }
    }

    /// <summary>
    /// Asynchronously saves the state of a bounded context and processes any resulting domain events.
    /// </summary>
    /// <remarks>This method checks for any events in the bounded context state and saves them to the event
    /// stream. If domain events are generated, they are posted to an event denormalizer for further
    /// processing.</remarks>
    /// <typeparam name="TState">The type of the bounded context state.</typeparam>
    /// <param name="boundedContextState">The state of the bounded context to be saved. Must implement <see cref="IBoundedContextState{TState}"/>.</param>
    /// <param name="command">The command associated with the bounded context state changes.</param>
    /// <returns></returns>
    /// <exception cref="StorageException">Thrown if an error occurs while saving the bounded context state.</exception>
    protected async Task SaveBoundedContextAsync<TState>(IBoundedContextState<TState> boundedContextState, ICommand command) 
        where TState : IBoundedContextState<TState>
    {
        try
        {
            // check for any bounded context state change events...
            if (boundedContextState.Events.Count  > 0)
            {
                var domainEvents = await _dbEventSource.SaveEventsAsync(command.StreamId, command.CommandId, boundedContextState.Events, async (e) =>await ValueTask.CompletedTask);
                _logger.LogInformationEvent($"{GetType().Name}", "saving bounded context: {BoundedContextName} with {EventsCount} domain events from command: {CommandName} to event stream: {StreamId}", typeof(TState).Name, domainEvents.Count, command.CommandName, command.StreamId);
                if (domainEvents!.Count > 0)
                {
                    _logger.LogInformationEvent($"{GetType().Name}", "posting {DomainEventsCount} domain events from command: {CommandName} to event denormalizer for bounded context: {BoundedContextName}", domainEvents.Count, command.CommandName, typeof(TState).Name);
                    await _eventDenormalizer.ExecuteAsync(domainEvents!);
                }
            }
        }
        catch(ConcurrencyException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var errorMsg = $"{GetType().Name}.SaveBoundedContextAsync failed for {typeof(TState).Name} ";
            _logger.LogError(ex, "{RepoName}.SaveBoundedContextAsync failed for {BoundedContextName}", GetType().Name, typeof(TState).Name);
            throw new StorageException(errorMsg, ex);
        }
    }

    /// <summary>
    /// Asynchronously posts domain events from the specified bounded context state to the event denormalizer.
    /// </summary>
    /// <remarks>This method logs the number of domain events being posted and handles exceptions by logging
    /// errors and rethrowing them.</remarks>
    /// <typeparam name="TState">The type of the bounded context state.</typeparam>
    /// <param name="aggState">The bounded context state containing the domain events to be posted. Cannot be null.</param>
    /// <returns></returns>
    /// <exception cref="StorageException">Thrown if an error occurs while posting events to the event denormalizer.</exception>
    protected async Task PostEventsAsync<TState>(IBoundedContextState<TState> aggState) 
        where TState : IBoundedContextState<TState>
    {
        try
        {
            // check for any bounded context state change events...
            if (aggState.Events.Count > 0)
            {
                _logger.LogInformationEvent($"{GetType().Name}", $"posting {aggState.Events.Count} domain events to event denormalizer for bounded context: {typeof(TState).Name}");
                await _eventDenormalizer.ExecuteAsync(aggState.Events);
            }
        }
        catch (StorageException ex)
        {
            _logger.LogError(ex, "{RepoName}.PostEventsAsync failed for {BoundedContextName}", GetType().Name, typeof(TState).Name);
            throw;
        }
        catch (Exception ex)
        {
            var errorMsg = $"{GetType().Name}.PostEventsAsync failed for {typeof(TState).Name}";
            _logger.LogError(ex, "{RepoName}.PostEventsAsync failed for {BoundedContextName}", GetType().Name, typeof(TState).Name);
            throw new StorageException(errorMsg, ex);
        }
    }

}
