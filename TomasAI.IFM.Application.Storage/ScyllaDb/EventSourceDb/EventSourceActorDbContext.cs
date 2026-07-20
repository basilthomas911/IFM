using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage.Postgres.SequenceIdDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Storage;
using CommandStatus = TomasAI.IFM.Application.Storage.Postgres.EventSourceDb.CommandStatus;

namespace TomasAI.IFM.Application.Storage.ScyllaDb.EventSourceDb;

/// <summary>
/// Database context for actor event-sourcing operations (load, save, delete, map-reduce) against the event store.
/// </summary>
/// <remarks>
/// <see cref="EventSourceActorDbContext"/> interacts with an event-sourced database to retrieve event streams, save
/// domain events, and manage event logs. It integrates with <see cref="IBlackboardService"/> for cached lookups
/// (e.g., stream and event name IDs) and <see cref="IDbContextFactory"/> for executing database commands.
/// Utility mapping methods convert data records to strongly-typed view models, and transactional save operations
/// ensure consistency.
/// </remarks>
/// <param name="connectionSettings">Application database connection settings.</param>
/// <param name="dbFactory">Factory providing access to event-source repository contexts.</param>
/// <param name="blackboardService">Blackboard service for cached ID resolution and lookups.</param>
/// <param name="logger">Logger for database provider diagnostics.</param>
public class EventSourceActorDbContext(IDbConnectionSettings connectionSettings, IDbContextFactory dbFactory, IBlackboardService blackboardService, ILogger<DbProvider> logger)
    : ObjectDataRepository<EventSourceActorDbContext>(connectionSettings[EventSourceActorDbConnection], logger), IEventSourceActorDbContext
{
    readonly IBlackboardService _blackboardService = IsArgumentNull.Set(blackboardService);
    readonly IDbContextFactory _dbFactory = IsArgumentNull.Set(dbFactory);

    /// <summary>
    /// Gets the database context.
    /// </summary>
    public override EventSourceActorDbContext Database => this;

    /// <summary>
    /// Configuration key for the event-source database connection.
    /// </summary>
    public const string EventSourceActorDbConnection = "EventSourceActorDbConnection";

    /// <summary>
    /// Error message used when loading events fails.
    /// </summary>
    public const string ERR_EventDbContext_LoadEventsAsync = "EventSourceActorDbContext: Unable to execute LoadEventsAsync";

    /// <summary>
    /// Error message used when saving events fails.
    /// </summary>
    public const string ERR_EventDbContext_SaveEventsAsync = "EventSourceActorDbContext: Unable to execute SaveEventsAsync";

    /// <summary>
    /// Maps data from an <see cref="IObjectDataRecord"/> to a <see cref="CommandLogReadModel"/> instance.
    /// </summary>
    /// <param name="e">The data record containing the source data for the mapping operation.</param>
    /// <returns>A <see cref="CommandLogReadModel"/> populated with values retrieved from the specified data record.</returns>
    internal static CommandLogReadModel MapToCommandLog<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new (
            CommandId: e.GetGuid(0),
            StreamId: e.GetString(1),
            AggregateName: e.GetEnum<BoundedContextName>(2),
            CommandName: e.GetString(3),
            CommandTimestamp: e.GetDateTime(4),
            CommandData: e.GetString(5)
        );

    /// <summary>
    /// Maps the provided object reader to an <see cref="EventStreamIdReadModel"/> instance.
    /// </summary>
    /// <param name="o">The object reader used to retrieve values for the <see cref="EventStreamIdReadModel"/> properties.</param>
    /// <returns>An <see cref="EventStreamIdReadModel"/> instance populated with values from the object reader.</returns>
    internal static EventStreamIdReadModel MapToEventStreamId<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new (
            EventStreamId: e.GetLong(0),
            EventStream: e.GetString(1)
        );  

    /// <summary>
    /// Maps data from an <see cref="IObjectDataRecord"/> to an instance of <see cref="EventNameIdReadModel"/>.
    /// </summary>
    /// <param name="e">The data record containing the source data for the mapping operation.</param>
    /// <returns>A new <see cref="EventNameIdReadModel"/> instance populated with values retrieved from the specified data record.</returns>
    internal static EventNameIdReadModel MapToEventNameId<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            EventNameId: e.GetInt(0),
            EventName: e.GetString(1),
            EventTypeName: e.GetString(2)
        );

    /// <summary>
    /// Maps data from an <see cref="IObjectDataRecord"/> to an <see cref="EventLogReadModel"/> instance.
    /// </summary>
    /// <remarks>This method performs a direct mapping of properties from the data record to the <see
    /// cref="EventLogReadModel"/>. Ensure that the data record contains valid data for all required
    /// properties.</remarks>
    /// <param name="e">A data record containing the source data for the mapping operation.  Each property of <see
    /// cref="EventLogReadModel"/> is populated using corresponding values retrieved from this record.</param>
    /// <returns>A new <see cref="EventLogReadModel"/> instance populated with data from the specified <see
    /// cref="IObjectDataRecord"/>.</returns>
    internal static EventLogReadModel MapToEventLog<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        =>  new  (
                EventStreamId: e.GetLong(0),
                EventName: e.GetString(1),
                EventTypeName: e.GetString(2),
                EventVersion: e.GetLong(3),
                EventData: e.GetString(4),
                CommandId: e.GetGuid(5),
                EventTimestamp: e.GetString(6)
            );

    /// <summary>
    /// Maps the specified data record to an <see cref="EventStreamReadModel"/> instance.
    /// </summary>
    /// <param name="e">The data record containing the event data to map.</param>
    /// <returns>An <see cref="EventStreamReadModel"/> populated with data from the data record.</returns>
    internal static EventStreamReadModel MapToEventStream<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new()
        {
            EventTypeName = e.GetString(2),
            EventVersion = e.GetLong(3),
            EventData = e.GetString(4)
        };

    static long MapToLong<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => e.GetLong(0);
    static int MapToInt<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => e.GetInt(0);

    /// <summary>
    /// Asynchronously retrieves the unique identifier for the specified event stream.
    /// </summary>
    /// <remarks>If the event stream does not exist, it will be created and assigned a new
    /// identifier.</remarks>
    /// <param name="eventStream">The name of the event stream for which the identifier is requested. Cannot be null or empty.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the unique identifier  of the
    /// specified event stream as a <see langword="long"/>.</returns>
    public async Task<long> GetEventStreamIdAsync(string eventStream)
    {
        return (await _blackboardService.EventStreamId.GetAsync(eventStream, InsertEntityTypeAsync)).EventStreamId;

        async Task<EventStreamIdReadModel> InsertEntityTypeAsync(string eventStream)
        {
            var eventStreamId = await InsertEventStreamAsync(eventStream);
            return new EventStreamIdReadModel(eventStreamId, eventStream);
        }
    }

    /// <summary>
    /// Saves a collection of domain events to the event stream asynchronously.
    /// </summary>
    /// <remarks>This method persists the provided domain events to the specified event stream and associates
    /// them with the given command ID. If any concurrency or storage-related issues occur during the operation,
    /// appropriate exceptions are thrown. The method ensures transactional integrity, rolling back changes in case of
    /// errors.</remarks>
    /// <param name="eventStream">The name of the event stream where the events will be saved. Cannot be null or empty.</param>
    /// <param name="commandId">The unique identifier of the command associated with the events. Used for tracking and correlation.</param>
    /// <param name="domainEvents">The collection of domain events to be saved. Cannot be null or empty.</param>
    /// <returns>A <see cref="DomainEventCollection"/> containing the saved domain events, including their updated identifiers.</returns>
    /// <exception cref="StorageException">Thrown if a storage-related error occurs during the operation.</exception>
    public async Task<DomainEventCollection> SaveEventsAsync( string eventStream, Guid commandId, DomainEventCollection domainEvents, Func<DomainEventCollection, ValueTask> denormalizer)
    {
        var savedEvents = new DomainEventCollection();
        List<(int EventNameId, IEvent DomainEvent)> eventLogParams = [];

        var streamId = await GetEventStreamIdAsync(eventStream);
        foreach (var e in domainEvents)
        {
            var eventNameId = await GetEventNameIdFromDomainEventAsync(e);
            eventLogParams.Add((eventNameId, e));
        }
        var db = _dbFactory.EventSourceDb;
        var tx = db.BeginTransaction();
        try
        {
            var eventDate = DateTime.Now;
            foreach (var e in eventLogParams)
            {
                EventInitHelper.SetProperty(e.DomainEvent, nameof(IEvent.EventId), await InsertEventLogAsync(streamId, e.EventNameId, e.DomainEvent.ToEventData(), commandId, eventDate));
                savedEvents.Add(e.DomainEvent);
            }
            await denormalizer(savedEvents);
            tx?.Commit();
        }
        catch (ConcurrencyException)
        {
            tx?.Rollback();
            throw;
        }
        catch (StorageException)
        {
            tx?.Rollback();
            throw;
        }
        catch (Exception ex)
        {
            tx?.Rollback();
            throw new StorageException(ERR_EventDbContext_SaveEventsAsync, ex);
        }
        return savedEvents;

    }

    /// <summary>
    /// Asynchronously inserts a log entry for the specified command into the event source database.
    /// </summary>
    /// <param name="command">The command to log. Must not be null.</param>
    /// <param name="commandTimestamp">The date and time, in UTC, when the command was issued.</param>
    /// <param name="commandData">A serialized representation of the command's data to be stored in the log. Cannot be null or empty.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task InsertCommandLogAsync(ICommand command, DateTime commandTimestamp, string commandData)
        => await _dbFactory.ActorEventSourceDb
                .Use(EventSourceDbCql.InsertCommandLog)
                .SetParameters(new InsertActorCommandLog(
                    commandId: command.CommandId,
                    streamId: command.StreamId,
                    aggregateName: $"{command.RouteTo}",
                    commandName: command.CommandName,
                    commandTimestamp: $"{commandTimestamp:o}",
                    commandStatus: $"{CommandStatus.InProgress}",
                    commandData: commandData
                ))
                .ExecuteCommandAsync();

    /// <summary>
    /// Asynchronously updates the log entry for a specified command with a new status and timestamp.
    /// </summary>
    /// <param name="commandId">The unique identifier of the command whose log entry is to be updated.</param>
    /// <param name="updateTimestamp">The date and time to record as the update timestamp for the command log entry.</param>
    /// <param name="commandStatus">The new status to assign to the command in the log.</param>
    /// <returns>A task that represents the asynchronous update operation.</returns>
    public async Task UpdateCommandLogAsync(Guid commandId, DateTime updateTimestamp, CommandStatus commandStatus)
        => await _dbFactory.ActorEventSourceDb
                .Use(EventSourceDbCql.UpdateCommandLog)
                .SetParameters(new UpdateCommandLog(
                    commandId: commandId,
                    commandStatus: $"{commandStatus}",
                    updateTimestamp: updateTimestamp
                ))
                .ExecuteCommandAsync();

    /// <summary>
    /// Deletes an event log entry from the database based on the specified event version.
    /// </summary>
    /// <remarks>This method performs an asynchronous database operation to delete the event log entry. Ensure
    /// that the specified <paramref name="eventVersion"/> corresponds to an existing event log.</remarks>
    /// <param name="eventVersion">The version of the event log to delete. Must be a positive integer.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task DeleteEventLogAsync(long eventVersion)
        => await _dbFactory.ActorEventSourceDb
            .Use(EventSourceDbCql.DeleteEventLog)
            .SetParameters(new DeleteEventLog(eventVersion))
            .ExecuteCommandAsync();

    /// <summary>
    /// Deletes event logs corresponding to the specified event versions asynchronously.
    /// </summary>
    /// <remarks>This method iterates through the provided event version identifiers and deletes each event
    /// log asynchronously. Ensure that the <paramref name="eventVersions"/> array is not null or empty to avoid
    /// unnecessary operations.</remarks>
    /// <param name="eventVersions">An array of event version identifiers to delete. Each identifier represents a specific event log.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task DeleteEventLogsAsync(long[] eventVersions)
    {
        foreach (var eventVersion in eventVersions)
            await DeleteEventLogAsync(eventVersion);
   }

    /// <summary>
    /// Asynchronously deletes all event log entries associated with the specified stream identifier.
    /// </summary>
    /// <param name="streamId">The unique identifier of the event stream whose log entries are to be deleted.</param>
    /// <returns>A task that represents the asynchronous delete operation.</returns>
    public async Task DeleteEventLogByStreamIdAsync(long streamId)
        => await _dbFactory.ActorEventSourceDb
            .Use(EventSourceDbCql.DeleteEventLogByStreamId)
            .SetParameters(new DeleteEventLogByStreamId(streamId))
            .ExecuteCommandAsync();

    /// <summary>
    /// Asynchronously deletes the event stream with the specified identifier from the data store.
    /// </summary>
    /// <param name="eventStreamId">The unique identifier of the event stream to delete.</param>
    /// <returns>A task that represents the asynchronous delete operation.</returns>
    public async Task DeleteEventStreamByIdAsync(long eventStreamId)
        => await _dbFactory.ActorEventSourceDb
            .Use(EventSourceDbCql.DeleteEventStreamById)
            .SetParameters(new DeleteEventStreamById(eventStreamId))
            .ExecuteCommandAsync();

    /// <summary>
    /// Retrieves the command log associated with the specified command ID.
    /// </summary>
    /// <remarks>This method asynchronously fetches the command log from the database using the provided
    /// command ID. Ensure that the <paramref name="commandId"/> is valid and corresponds to an existing
    /// command.</remarks>
    /// <param name="commandId">The unique identifier of the command whose log is to be retrieved.</param>
    /// <returns>A <see cref="CommandLogReadModel"/> representing the command log if found; otherwise, <see langword="null"/>.</returns>
    internal async Task<CommandLogReadModel?> GetCommandLogAsync(Guid commandId)
        =>  await _dbFactory.ActorEventSourceDb
            .Use(EventSourceDbCql.GetCommandLog)
            .SetParameters(new GetCommandLog(commandId))
            .ExecuteSingleAsync<CommandLogReadModel>(MapToCommandLog);

    /// <summary>
    /// Asynchronously retrieves the unique identifier for the specified event stream.
    /// </summary>
    /// <remarks>If the event stream does not exist, it will be created and its identifier will be
    /// returned.</remarks>
    /// <param name="eventStream">The name of the event stream to retrieve the identifier for. Cannot be null or empty.</param>
    /// <returns>A <see langword="long"/> representing the unique identifier of the specified event stream.</returns>
    internal async Task<long> GetEventStreamAsync(string eventStream)
    {
        return (await _blackboardService.EventStreamId.GetAsync(eventStream, InsertEventStream)).EventStreamId;

        async Task<EventStreamIdReadModel> InsertEventStream(string eventStream)
        {
            var eventStreamId = await InsertEventStreamAsync(eventStream);
            return new EventStreamIdReadModel(eventStreamId, eventStream);
        }
    }

    /// <summary>
    /// Retrieves the event stream ID from the database based on the specified event stream name.
    /// </summary>
    /// <remarks>This method queries the database to retrieve the ID associated with the given event stream
    /// name. Ensure that the provided <paramref name="eventStream"/> corresponds to a valid entry in the
    /// database.</remarks>
    /// <param name="eventStream">The name of the event stream to query. Cannot be null or empty.</param>
    /// <returns>An <see cref="EventStreamIdReadModel"/> containing the event stream ID if found; otherwise, <see
    /// langword="null"/>.</returns>
    public async Task<EventStreamIdReadModel?> GetEventStreamIdFromDbAsync(string eventStream)
        => await _dbFactory.ActorEventSourceDb
            .Use(EventSourceDbCql.GetEventStreamId)
            .SetParameters(new GetEventStreamId(eventStream))
            .ExecuteSingleAsync<EventStreamIdReadModel>(MapToEventStreamId);

    /// <summary>
    /// Inserts an event stream into the database if it does not already exist.
    /// </summary>
    /// <remarks>If the specified event stream already exists in the database, the method retrieves its
    /// identifier and returns it. Otherwise, the method deletes any existing references to the event stream and inserts
    /// a new record, returning the newly generated identifier.</remarks>
    /// <param name="eventStream">The name of the event stream to insert. This value cannot be null or empty.</param>
    /// <returns>The unique identifier of the event stream. If the event stream already exists, its existing identifier is
    /// returned; otherwise, the identifier of the newly inserted event stream is returned.</returns>
    internal async Task<long> InsertEventStreamAsync(string eventStream)
    {
        var eventStreamIdModel = await GetEventStreamIdFromDbAsync(eventStream);
        if (eventStreamIdModel is not null)
            return eventStreamIdModel.EventStreamId;

        await _dbFactory.ActorEventSourceDb
            .Use(EventSourceDbCql.DeleteEventStreamId)
            .SetParameters(new DeleteEventStreamId(eventStream))
            .ExecuteCommandAsync();

        var eventStreamId = _blackboardService.SequenceCounter.Increment(SequenceIdType.EventStreamId);
        return await _dbFactory.ActorEventSourceDb
            .Use(EventSourceDbCql.InsertEventStreamId)
            .SetParameters(new InsertEventStreamId(eventStreamId,eventStream))
            .ExecuteScalarAsync(MapToLong);
    }
        
   /// <summary>
   /// Retrieves the unique identifier associated with the event name of the specified domain event type.
   /// </summary>
   /// <typeparam name="TEvent">The type of the domain event, which must implement <see cref="IEvent"/>.</typeparam>
   /// <param name="domainEvent">The domain event instance whose event name identifier is to be retrieved. Cannot be <see langword="null"/>.</param>
   /// <returns>A task that represents the asynchronous operation. The task result contains the unique identifier for the event
   /// name associated with the specified domain event type.</returns>
    public async Task<int> GetEventNameIdFromDomainEventAsync<TEvent>(TEvent domainEvent) where TEvent : IEvent
        =>  await GetEventNameIdFromTypeAsync(domainEvent.GetType());

    /// <summary>
    /// Asynchronously retrieves the unique identifier for the event name associated with the specified event type.
    /// </summary>
    /// <typeparam name="TEvent">The type of the event, which must implement the <see cref="IEvent"/> interface.</typeparam>
    /// <returns>A task that represents the asynchronous operation. The task result contains the unique identifier  for the event
    /// name associated with the specified event type.</returns>
    internal async Task<int> GetEventNameIdFromTypeAsync<TEvent>() where TEvent : IEvent
        =>  await GetEventNameIdFromTypeAsync(typeof(TEvent));

    /// <summary>
    /// Asynchronously retrieves the unique identifier for an event name based on the specified event type.
    /// </summary>
    /// <remarks>This method interacts with a data source to retrieve or insert the event name identifier. If
    /// the event name  identifier does not exist, it will be created and stored in the database.</remarks>
    /// <param name="eventType">The <see cref="Type"/> of the event for which the identifier is to be retrieved.  The <see
    /// cref="Type.FullName"/> and <see cref="Type.Name"/> properties are used to determine the event name and type.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the unique identifier  for the event
    /// name associated with the specified event type.</returns>
    async Task<int> GetEventNameIdFromTypeAsync(Type eventType)
    {
        var eventTypeFullName = string.IsNullOrEmpty(eventType.FullName) ? string.Empty : $"{eventType.FullName}";
        return (await _blackboardService.EventNameId.GetAsync(eventType.Name, eventTypeFullName, InsertEventNameIdAsync)).EventNameId;

        async Task<EventNameIdReadModel> InsertEventNameIdAsync(string eventName, string eventTypeName)
        {
            var eventNameIdModel = await GetEventNameIdFromDbAsync(eventName);
            if (eventNameIdModel.IsValid)
                return eventNameIdModel;
            await _dbFactory.ActorEventSourceDb
                  .Use(EventSourceDbCql.DeleteEventNameId)
                  .SetParameters(new DeleteEventNameId(eventName, eventTypeName))
                  .ExecuteCommandAsync();
            var eventNameId = await _dbFactory.ActorEventSourceDb
                  .Use(EventSourceDbCql.InsertEventNameId)
                  .SetParameters(new InsertEventNameId(eventName, eventTypeName))
                  .ExecuteScalarAsync(MapToInt);
            return new EventNameIdReadModel(eventNameId, eventName, eventTypeName);
        }
    }

    /// <summary>
    /// Retrieves the event name and its associated ID from the database asynchronously.
    /// </summary>
    /// <remarks>This method queries the database using the provided event name and maps the result to an <see
    /// cref="EventNameIdReadModel"/>. Ensure the database connection is properly configured before calling this
    /// method.</remarks>
    /// <param name="eventName">The name of the event to look up in the database. Cannot be null or empty.</param>
    /// <returns>An <see cref="EventNameIdReadModel"/> containing the event name and ID if the event is found; otherwise, <see
    /// langword="null"/>.</returns>
    internal async Task<EventNameIdReadModel> GetEventNameIdFromDbAsync(string eventName)
        => await _dbFactory.ActorEventSourceDb
            .Use(EventSourceDbCql.GetEventNameId)
            .SetParameters(new GetEventNameId(eventName))
            .ExecuteSingleAsync<EventNameIdReadModel>(MapToEventNameId);

    /// <summary>
    /// Inserts a new event log entry into the database asynchronously.
    /// </summary>
    /// <remarks>This method performs an asynchronous database operation to insert an event log entry. Ensure
    /// that the provided parameters are valid and consistent with the database schema.</remarks>
    /// <param name="eventStreamId">The unique identifier of the event stream to which the event belongs.</param>
    /// <param name="eventNameId">The identifier of the event name, representing the type or category of the event.</param>
    /// <param name="eventData">The serialized data associated with the event. This cannot be null or empty.</param>
    /// <param name="commandId">The unique identifier of the command that triggered the event.</param>
    /// <param name="eventTimestamp">The timestamp of the event, in UTC, formatted as an ISO 8601 string.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the unique identifier of the newly
    /// inserted event log entry.</returns>
    internal async Task<long> InsertEventLogAsync(long eventStreamId, int eventNameId, string eventData, Guid commandId, DateTime eventTimestamp)
        => await _dbFactory.ActorEventSourceDb
                .Use(EventSourceDbCql.InsertEventLog)
                .SetParameters(new InsertEventLog(eventStreamId, eventNameId, eventData, commandId, $"{eventTimestamp:o}"))
                .ExecuteScalarAsync(MapToLong);

    /// <summary>
    /// Retrieves a collection of domain events associated with the specified event stream ID.
    /// </summary>
    /// <remarks>This method queries the underlying event source database to retrieve event logs associated
    /// with the given event stream ID. The event logs are then mapped to domain events and returned as a
    /// collection.</remarks>
    /// <param name="eventStreamId">The unique identifier of the event stream for which domain events are to be retrieved. Must be a positive
    /// integer.</param>
    /// <returns>An <see cref="ICollection{T}"/> of <see cref="EventStreamReadModel"/> representing the event stream entries for the
    /// specified stream. If no events are found, the collection will be empty.</returns>
    internal async ValueTask<ICollection<EventStreamReadModel>> GetEventStreamAsync(long eventStreamId)
        => await _dbFactory.ActorEventSourceDb
                .Use(EventSourceDbCql.GetEventLogByEventStreamId)
                .SetParameters(new GetEventLogByEventStreamId(eventStreamId))
                .ExecuteQueryAsync<EventStreamReadModel>(MapToEventStream);

    /// <summary>
    /// Asynchronously retrieves the last N events from a specified event stream.
    /// </summary>
    /// <remarks>The method queries the event source database to fetch the specified number of recent events 
    /// from the given event stream. The events are returned in ascending order of their version.</remarks>
    /// <param name="eventStreamId">The unique identifier of the event stream from which to retrieve events.</param>
    /// <param name="lastNRange">The number of most recent events to retrieve from the event stream.</param>
    /// <returns>An <see cref="ICollection{T}"/> of <see cref="EventStreamReadModel"/> representing the last N events, ordered by
    /// event version. Returns an empty collection if no events are found.</returns>
    internal async ValueTask<ICollection<EventStreamReadModel>> GetEventsLastNRangeAsync(long eventStreamId, int lastNRange)
    {
        var eventLogRange = await _dbFactory.ActorEventSourceDb
            .Use(EventSourceDbCql.GetEventLogLastNRange)
            .SetParameters(new GetEventLogLastNRange(eventStreamId))
            .ExecuteQueryAsync<EventStreamReadModel>(MapToEventStream);
        return (eventLogRange is null || eventLogRange.Count == 0)
            ? [] : [.. eventLogRange.Take(lastNRange).OrderBy(e => e.EventVersion)];
    }

    /// <summary>
    /// Asynchronously retrieves a collection of event stream view models from a snapshot for a specified event stream.
    /// </summary>
    /// <remarks>If the maximum event version is greater than zero, the method retrieves events up to that
    /// version. Otherwise, it retrieves all events associated with the specified event stream identifier.</remarks>
    /// <typeparam name="TSnapshot">The type of the snapshot event, which must implement <see cref="IEvent"/>.</typeparam>
    /// <param name="eventStreamId">The identifier of the event stream from which to retrieve events.</param>
    /// <returns>An <see cref="ICollection{T}"/> of <see cref="EventStreamReadModel"/> instances representing the events in the
    /// specified event stream.</returns>
    public async ValueTask<ICollection<EventStreamReadModel>> GetEventsFromSnapshotAsync<TSnapshot>(long eventStreamId) 
        where TSnapshot : IEvent
    {
        var snapshotEventNameId = await GetEventNameIdFromTypeAsync<TSnapshot>();
        var db = _dbFactory.ActorEventSourceDb;
        var maxEventVersion = await db.Use(EventSourceDbCql.GetMaxEventVersion)
            .SetParameters(new GetMaxEventVersion(eventStreamId, snapshotEventNameId))
            .ExecuteScalarAsync(MapToLong);
        return maxEventVersion > 0
            ? await db.Use(EventSourceDbCql.GetEventLogByMaxEventVersion)
                .SetParameters(new GetEventLogByMaxEventVersion(eventStreamId, maxEventVersion))
                .ExecuteQueryAsync(MapToEventStream)
            : await db.Use(EventSourceDbCql.GetEventLogByEventStreamId)
                .SetParameters(new GetEventLogByEventStreamId(eventStreamId))
                .ExecuteQueryAsync(MapToEventStream);
    }

    /// <summary>
    /// Processes the full event stream by mapping records and invoking the provided reducer action.
    /// </summary>
    /// <typeparam name="TState">The actor state type that implements <see cref="IActorState{TState}"/>.</typeparam>
    /// <param name="eventStreamId">The unique identifier of the event stream to process.</param>
    /// <param name="reducerAction">The action invoked with the mapped <see cref="EventStreamReadModel"/> sequence.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    public async ValueTask MapReduceActorEventStreamAsync<TState>(long eventStreamId, Action<IEnumerable<EventStreamReadModel>> reducerAction) where TState : IActorState<TState>
    {
        var eventStream = new EventStreamReadModel();
        await _dbFactory.ActorEventSourceDb
                .Use(EventSourceDbCql.GetEventLogByEventStreamId)
                .SetParameters(new GetEventLogByEventStreamId(eventStreamId))
                .ExecuteMapReduceAsync(EventStreamMapper, reducerAction);

        EventStreamReadModel EventStreamMapper(IObjectDataRecord e)
        {
            eventStream.EventVersion = e.GetLong(3);
            eventStream.EventTypeName = e.GetString(2);
            eventStream.EventData = e.GetString(4);
            return eventStream;
        }
    }

    /// <summary>
    /// Processes the last N events in an event stream and invokes the reducer action on the ordered subset.
    /// </summary>
    /// <typeparam name="TState">The actor state type that implements <see cref="IActorState{TState}"/>.</typeparam>
    /// <typeparam name="TEvent">The event type used to filter the stream, implementing <see cref="IEvent"/>.</typeparam>
    /// <param name="eventStreamId">The unique identifier of the event stream to process.</param>
    /// <param name="lastNRange">The number of most recent events to include.</param>
    /// <param name="reducerAction">The action invoked with the ordered subset of <see cref="EventStreamReadModel"/> entries.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    public async ValueTask MapReduceActorEventStreamAsync<TState, TEvent>(long eventStreamId, int lastNRange, Action<IEnumerable<EventStreamReadModel>> reducerAction)
        where TState : IActorState<TState>
        where TEvent : IEvent
    {
        var eventStream = new EventStreamReadModel();
        await _dbFactory.ActorEventSourceDb
            .Use(EventSourceDbCql.GetEventLogLastNRange)
            .SetParameters(new GetEventLogLastNRange(eventStreamId))
            .ExecuteMapReduceAsync<EventStreamReadModel>(EventStreamMapper,
                reducer => reducerAction.Invoke(reducer.Take(lastNRange).OrderBy(e => e.EventVersion)));

        EventStreamReadModel EventStreamMapper(IObjectDataRecord e)
        {
            eventStream.EventVersion = e.GetLong(3);
            eventStream.EventTypeName = e.GetString(2);
            eventStream.EventData = e.GetString(4);
            return eventStream;
        }
    }

    /// <summary>
    /// Processes an event stream starting from the latest snapshot event type and invokes the reducer action.
    /// </summary>
    /// <typeparam name="TState">The actor state type that implements <see cref="IActorState{TState}"/>.</typeparam>
    /// <typeparam name="TSnapshot">The snapshot event type, implementing <see cref="IEvent"/>.</typeparam>
    /// <param name="eventStreamId">The unique identifier of the event stream to process.</param>
    /// <param name="reducerAction">The action invoked with the mapped <see cref="EventStreamReadModel"/> sequence.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    public async ValueTask MapReduceActorEventStreamAsync<TState, TSnapshot>(long eventStreamId, Action<IEnumerable<EventStreamReadModel>> reducerAction)
        where TState : IActorState<TState>
        where TSnapshot : IEvent
    {
        var eventStream = new EventStreamReadModel();
        var snapshotEventNameId = await GetEventNameIdFromTypeAsync<TSnapshot>();
        var db = _dbFactory.ActorEventSourceDb;
        var maxEventVersion = await db.Use(EventSourceDbCql.GetMaxEventVersion)
            .SetParameters(new GetMaxEventVersion(eventStreamId, snapshotEventNameId))
            .ExecuteScalarAsync(MapToLong);
        if (maxEventVersion > 0)
            await db.Use(EventSourceDbCql.GetEventLogByMaxEventVersion)
                .SetParameters(new GetEventLogByMaxEventVersion(eventStreamId, maxEventVersion))
                .ExecuteMapReduceAsync(EventStreamMapper, reducerAction);
        else
            await db.Use(EventSourceDbCql.GetEventLogByEventStreamId)
                .SetParameters(new GetEventLogByEventStreamId(eventStreamId))
                .ExecuteMapReduceAsync(EventStreamMapper, reducerAction);

        EventStreamReadModel EventStreamMapper(IObjectDataRecord e)
        {
            eventStream.EventVersion = e.GetLong(3);
            eventStream.EventTypeName = e.GetString(2);
            eventStream.EventData = e.GetString(4);
            return eventStream;
        }
    }

    /// <summary>
    /// Loads the full event stream into memory for the specified actor state type.
    /// </summary>
    /// <typeparam name="TState">The actor state type that implements <see cref="IActorState{TState}"/>.</typeparam>
    /// <param name="eventStreamId">The unique identifier of the event stream to load.</param>
    /// <returns>An <see cref="ICollection{T}"/> of <see cref="EventStreamReadModel"/> entries for the stream.</returns>
    public async ValueTask<ICollection<EventStreamReadModel>> LoadActorEventStreamAsync<TState>(long eventStreamId) 
        where TState : IActorState<TState>
            => await GetEventStreamAsync(eventStreamId);

    /// <summary>
    /// Loads the last N events of an event stream into memory for the specified actor state type.
    /// </summary>
    /// <typeparam name="TState">The actor state type that implements <see cref="IActorState{TState}"/>.</typeparam>
    /// <typeparam name="TEvent">The event type used to filter the stream, implementing <see cref="IEvent"/>.</typeparam>
    /// <param name="eventStreamId">The unique identifier of the event stream to load.</param>
    /// <param name="lastNRange">The number of most recent events to include.</param>
    /// <returns>An <see cref="ICollection{T}"/> of <see cref="EventStreamReadModel"/> entries ordered by version.</returns>
    public async ValueTask<ICollection<EventStreamReadModel>> LoadActorEventStreamAsync<TState, TEvent>(long eventStreamId, int lastNRange)
        where TState : IActorState<TState>
        where TEvent : IEvent
          => await GetEventsLastNRangeAsync(eventStreamId, lastNRange);

    /// <summary>
    /// Loads an event stream starting from the latest snapshot of the specified type.
    /// </summary>
    /// <typeparam name="TState">The actor state type that implements <see cref="IActorState{TState}"/>.</typeparam>
    /// <typeparam name="TSnapshot">The snapshot event type, implementing <see cref="IEvent"/>.</typeparam>
    /// <param name="eventStreamId">The unique identifier of the event stream to load.</param>
    /// <returns>An <see cref="ICollection{T}"/> of <see cref="EventStreamReadModel"/> entries representing the snapshot-based stream.</returns>
    public async ValueTask<ICollection<EventStreamReadModel>> LoadActorEventStreamAsync<TState, TSnapshot>(long eventStreamId)
        where TState : IActorState<TState>
        where TSnapshot : IEvent
           => await GetEventsFromSnapshotAsync<TSnapshot>(eventStreamId);

}
