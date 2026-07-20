using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Linq.Expressions;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.SetParameters;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;

/// <summary>
/// Provides a specialized database context for managing event sourcing operations, including loading, saving, and
/// deleting events.
/// </summary>
/// <remarks>The <see cref="EventSourceDbContext"/> class is designed to interact with an event-sourced database,
/// enabling operations such as retrieving event streams, saving domain events, and managing event logs. It integrates
/// with services such as <see cref="IBlackboardService"/> and <see cref="IDbContextFactory"/> to facilitate database
/// operations and event stream management. <para> This class also provides utility methods for mapping database records
/// to view models and supports transactional operations for saving events. </para></remarks>
/// <param name="connectionSettings"></param>
/// <param name="dbFactory"></param>
/// <param name="blackboardService"></param>
/// <param name="logger"></param>
public class EventSourceDbContext(IDbConnectionSettings connectionSettings, IDbContextFactory dbFactory, IBlackboardService blackboardService, ILogger<DbProvider> logger)
    : ObjectDataRepository<EventSourceDbContext>(connectionSettings[EventSourceDbConnection], logger), IEventSourceDbContext
{
    readonly IBlackboardService _blackboardService = IsArgumentNull.Set(blackboardService);
    readonly IDbContextFactory _dbFactory = IsArgumentNull.Set(dbFactory);

    public const string EventSourceDbConnection = "EventSourceDbConnection";
    public const string ERR_EventDbContext_LoadEventsAsync = "EventSourceDbContext: Unable to execute LoadEventsAsync";
    public const string ERR_EventDbContext_SaveEventsAsync = "EventSourceDbContext: Unable to execute SaveEventsAsync";

    /// <summary>
    /// Gets the database context.
    /// </summary>
    public override EventSourceDbContext Database => this;

    /// <summary>
    /// Maps data from an <see cref="IObjectMapReader{T}"/> to a <see cref="CommandLogReadModel"/> instance.
    /// </summary>
    /// <param name="o">The object map reader containing the source data for the mapping operation.</param>
    /// <returns>A <see cref="CommandLogReadModel"/> populated with values retrieved from the specified object map reader.</returns>
    internal static CommandLogReadModel MapToCommandLog(IObjectMapReader<CommandLogReadModel> o)
        => new (
            CommandId: o.Get(e => e.CommandId),
            StreamId: o.Get(e => e.StreamId),
            AggregateName: o.Get(e => e.AggregateName),
            CommandName: o.Get(e => e.CommandName),
            CommandTimestamp: o.Get(e => e.CommandTimestamp),
            CommandData: o.Get(e => e.CommandData)
        );

    /// <summary>
    /// Maps the provided object reader to an <see cref="EventStreamIdReadModel"/> instance.
    /// </summary>
    /// <param name="o">The object reader used to retrieve values for the <see cref="EventStreamIdReadModel"/> properties.</param>
    /// <returns>An <see cref="EventStreamIdReadModel"/> instance populated with values from the object reader.</returns>
    internal static EventStreamIdReadModel MapToEventStreamId(IObjectMapReader<EventStreamIdReadModel> o)
        => new (
            EventStreamId: o.Get(e => e.EventStreamId),
            EventStream: o.Get(e => e.EventStream)
        );

    /// <summary>
    /// Maps data from an <see cref="IObjectMapReader{T}"/> to an instance of <see cref="EventNameIdReadModel"/>.
    /// </summary>
    /// <param name="o">The object map reader containing the source data for the mapping operation.</param>
    /// <returns>A new <see cref="EventNameIdReadModel"/> instance populated with values retrieved from the specified object map
    /// reader.</returns>
    internal static EventNameIdReadModel MapToEventNameId(IObjectMapReader<EventNameIdReadModel> o)
        => new(
                EventNameId: o.Get(_eventNameIdExpr),
                EventName: o.Get(_eventNameExpr),
                EventTypeName: o.Get(_eventTypeNameExpr)
            );
    static readonly Expression<Func<EventNameIdReadModel, int>> _eventNameIdExpr = e => e.EventNameId;
    static readonly Expression<Func<EventNameIdReadModel, string>> _eventNameExpr = e => e.EventName;
    static readonly Expression<Func<EventNameIdReadModel, string>> _eventTypeNameExpr = e => e.EventTypeName;

    /// <summary>
    /// Maps data from an <see cref="IObjectMapReader{T}"/> to an <see cref="EventLogReadModel"/> instance.
    /// </summary>
    /// <remarks>This method performs a direct mapping of properties from the object map reader to the <see
    /// cref="EventLogReadModel"/>. Ensure that the object map reader contains valid data for all required
    /// properties.</remarks>
    /// <param name="o">An object map reader containing the source data for the mapping operation.  Each property of <see
    /// cref="EventLogReadModel"/> is populated using corresponding values retrieved from this reader.</param>
    /// <returns>A new <see cref="EventLogReadModel"/> instance populated with data from the specified <see
    /// cref="IObjectMapReader{T}"/>.</returns>
    internal static EventLogReadModel MapToEventLog(IObjectMapReader<EventLogReadModel> o)
        =>  new  (
                EventStreamId: o.Get(e => e.EventStreamId),
                EventName: o.Get(e => e.EventName),
                EventTypeName: o.Get(e => e.EventTypeName),
                EventVersion: o.Get(e => e.EventVersion),
                EventData: o.Get(e => e.EventData),
                CommandId: o.Get(e => e.CommandId),
                EventTimestamp: o.Get(e => e.EventTimestamp)
            );

    /// <summary>
    /// Maps the specified object map reader to an <see cref="EventStreamReadModel"/> instance.
    /// </summary>
    /// <param name="o">The object map reader containing the event data to map.</param>
    /// <returns>An <see cref="EventStreamReadModel"/> populated with data from the object map reader.</returns>
    internal static EventStreamReadModel MapToEventStream(IObjectMapReader<EventStreamReadModel> o)
        => new()
        {
            EventTypeName = o.Get(e => e.EventTypeName),
            EventVersion = o.Get(e => e.EventVersion),
            EventData = o.Get(e => e.EventData)
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
    public async ValueTask<long> GetEventStreamIdAsync(string eventStream)
    {
        return (await _blackboardService.EventStreamId.GetAsync(eventStream, InsertEntityTypeAsync)).EventStreamId;

        async Task<EventStreamIdReadModel> InsertEntityTypeAsync(string eventStream)
        {
            var eventStreamId = await InsertEventStreamAsync(eventStream);
            return new EventStreamIdReadModel(eventStreamId, eventStream);
        }
    }

    /// <summary>
    /// Asynchronously loads a collection of event data for a specified event stream.
    /// </summary>
    /// <typeparam name="TBoundedContext">The type of the bounded context, which must implement <see cref="IBoundedContext"/>.</typeparam>
    /// <param name="eventStreamId">The identifier of the event stream to load events from.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a collection of <see
    /// cref="EventStreamReadModel"/> representing the events in the specified stream.</returns>
    public async ValueTask<ICollection<EventStreamReadModel>> LoadEventStreamAsync<TBoundedContext>(long eventStreamId) 
        where TBoundedContext : IBoundedContext
        => await GetEventStreamAsync(eventStreamId);

    /// <summary>
    /// Asynchronously processes an event stream by applying a reduction action to the events.
    /// </summary>
    /// <remarks>This method retrieves events associated with the specified <paramref name="eventStreamId"/>
    /// and applies the provided <paramref name="reducerAction"/> to them.</remarks>
    /// <typeparam name="TBoundedContext">The type of the bounded context that implements <see cref="IBoundedContext"/>.</typeparam>
    /// <param name="eventStreamId">The identifier of the event stream to be processed.</param>
    /// <param name="reducerAction">The action to apply to the collection of <see cref="EventStreamReadModel"/> instances representing the event
    /// stream.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    public async ValueTask MapReduceEventStreamAsync<TBoundedContext>(long eventStreamId, Action<IEnumerable<EventStreamReadModel>> reducerAction)
        where TBoundedContext : IBoundedContext
    {
        var eventStream = new EventStreamReadModel();
        await _dbFactory.EventSourceDb
                .Use(EventSourceDbSql.GetEventLogByEventStreamId)
                .SetParameters(new GetEventLogByEventStreamId(eventStreamId))
                .ExecuteMapReduceAsync(EventStreamMapper, reducerAction);

        EventStreamReadModel EventStreamMapper(IObjectMapReader<EventStreamReadModel> o)
        {
            eventStream.EventVersion = o.Get(e => e.EventVersion);
            eventStream.EventTypeName = o.Get(e => e.EventTypeName);
            eventStream.EventData = o.Get(e => e.EventData);
            return eventStream;
        }

    }

    /// <summary>
    /// Asynchronously loads a collection of domain events from the specified event stream.
    /// </summary>
    /// <remarks>This method retrieves events in reverse chronological order, starting from the most recent
    /// event in the stream. If the event stream contains fewer than <paramref name="lastNRange"/> events, all available
    /// events will be returned.</remarks>
    /// <typeparam name="TBoundedContext">The type of the bounded context associated with the event stream. Must implement <see cref="IBoundedContext"/>.</typeparam>
    /// <typeparam name="TEvent">The type of the events to load. Must implement <see cref="IEvent"/>.</typeparam>
    /// <param name="eventStreamId">The unique identifier of the event stream from which to load events.</param>
    /// <param name="lastNRange">The number of most recent events to retrieve from the event stream. Must be a positive integer.</param>
    /// <returns>A <see cref="DomainEventCollection"/> containing the most recent events from the specified event stream.</returns>
    public async ValueTask<ICollection<EventStreamReadModel>> LoadEventStreamAsync<TBoundedContext, TEvent>(long eventStreamId, int lastNRange)
        where TBoundedContext : IBoundedContext
        where TEvent : IEvent
        =>await GetEventsLastNRangeAsync(eventStreamId, lastNRange);

    /// <summary>
    /// Asynchronously processes a stream of events by applying a map-reduce operation.
    /// </summary>
    /// <remarks>This method retrieves the last <paramref name="lastNRange"/> events from the specified event
    /// stream, orders them by event version, and applies the provided <paramref name="reducerAction"/> to the
    /// result.</remarks>
    /// <typeparam name="TBoundedContext">The type of the bounded context, which must implement <see cref="IBoundedContext"/>.</typeparam>
    /// <typeparam name="TEvent">The type of the event, which must implement <see cref="IEvent"/>.</typeparam>
    /// <param name="eventStreamId">The identifier of the event stream to be processed.</param>
    /// <param name="lastNRange">The number of most recent events to include in the reduction operation.</param>
    /// <param name="reducerAction">An action to perform on the reduced collection of <see cref="EventStreamReadModel"/> instances.</param>
    /// <returns></returns>
    public async ValueTask MapReduceEventStreamAsync<TBoundedContext, TEvent>(long eventStreamId, int lastNRange,  Action<IEnumerable<EventStreamReadModel>> reducerAction)
        where TBoundedContext : IBoundedContext
        where TEvent : IEvent
    {
        var eventStream = new EventStreamReadModel();
        await _dbFactory.EventSourceDb
            .Use(EventSourceDbSql.GetEventLogLastNRange)
            .SetParameters(new GetEventLogLastNRange(eventStreamId))
            .ExecuteMapReduceAsync<EventStreamReadModel>(EventStreamMapper,
                reducer => reducerAction.Invoke(reducer.Take(lastNRange).OrderBy(e => e.EventVersion)));

        EventStreamReadModel EventStreamMapper(IObjectMapReader<EventStreamReadModel> o)
        {
            eventStream.EventVersion = o.Get(e => e.EventVersion);
            eventStream.EventTypeName = o.Get(e => e.EventTypeName);
            eventStream.EventData = o.Get(e => e.EventData);
            return eventStream;
        }

    }

    /// <summary>
    /// Asynchronously loads a collection of domain events for the specified event stream.
    /// </summary>
    /// <remarks>This method retrieves domain events by leveraging snapshot-based event sourcing. Ensure that
    /// the specified <typeparamref name="TSnapshot"/> type corresponds to the snapshot structure used in the event
    /// stream.</remarks>
    /// <typeparam name="TBoundedContext">The type of the bounded context associated with the event stream. Must implement <see cref="IBoundedContext"/>.</typeparam>
    /// <typeparam name="TSnapshot">The type of the snapshot event used to retrieve the domain events. Must implement <see cref="IEvent"/>.</typeparam>
    /// <param name="eventStreamId">The unique identifier of the event stream to load events from.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a <see
    /// cref="DomainEventCollection"/> containing the domain events associated with the specified event stream.</returns>
    public async ValueTask<ICollection<EventStreamReadModel>> LoadEventStreamAsync<TBoundedContext, TSnapshot>(long eventStreamId)
        where TBoundedContext : IBoundedContext
        where TSnapshot : IEvent
        => await GetEventsFromSnapshotAsync<TSnapshot>(eventStreamId);

    /// <summary>
    /// Asynchronously processes an event stream by applying a map-reduce operation.
    /// </summary>
    /// <remarks>This method retrieves the maximum event version for the specified event stream and applies a
    /// map-reduce operation using the provided <paramref name="reducerAction"/>. If no events are found with a version
    /// greater than zero, it processes the event stream by its identifier.</remarks>
    /// <typeparam name="TBoundedContext">The type of the bounded context, which must implement <see cref="IBoundedContext"/>.</typeparam>
    /// <typeparam name="TSnapshot">The type of the snapshot event, which must implement <see cref="IEvent"/>.</typeparam>
    /// <param name="eventStreamId">The unique identifier of the event stream to be processed.</param>
    /// <param name="reducerAction">An action to be executed on the reduced set of <see cref="EventStreamReadModel"/> instances.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    public async ValueTask MapReduceEventStreamAsync<TBoundedContext, TSnapshot>(long eventStreamId, Action<IEnumerable<EventStreamReadModel>> reducerAction)
     where TBoundedContext : IBoundedContext
     where TSnapshot : IEvent
    {
        var eventStream = new EventStreamReadModel();
        var snapshotEventNameId = await GetEventNameIdFromTypeAsync<TSnapshot>();
        var db = _dbFactory.EventSourceDb;
        var maxEventVersion = await db.Use(EventSourceDbSql.GetMaxEventVersion)
            .SetParameters(new GetMaxEventVersion(eventStreamId, snapshotEventNameId))
            .ExecuteScalarAsync(MapToLong);
        if (maxEventVersion > 0)
            await db.Use(EventSourceDbSql.GetEventLogByMaxEventVersion)
                .SetParameters(new GetEventLogByMaxEventVersion(eventStreamId, maxEventVersion))
                .ExecuteMapReduceAsync(EventStreamMapper, reducerAction);
        else
            await db.Use(EventSourceDbSql.GetEventLogByEventStreamId)
                .SetParameters(new GetEventLogByEventStreamId(eventStreamId))
                .ExecuteMapReduceAsync(EventStreamMapper, reducerAction);

        EventStreamReadModel EventStreamMapper(IObjectMapReader<EventStreamReadModel> o)
        {
            eventStream.EventVersion = o.Get(e => e.EventVersion);
            eventStream.EventTypeName = o.Get(e => e.EventTypeName);
            eventStream.EventData = o.Get(e => e.EventData);
            return eventStream;
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
    /// Inserts a log entry for the specified command into the database asynchronously.
    /// </summary>
    /// <remarks>This method uses a stored procedure to insert the log entry into the database. The returned
    /// identifier can be used to reference the log entry in subsequent operations.</remarks>
    /// <param name="command">The command to log. Must not be <see langword="null"/>. The <see cref="ICommand.CommandId"/>, <see
    /// cref="ICommand.StreamId"/>, <see cref="ICommand.RouteTo"/>, and <see cref="ICommand.CommandName"/> properties
    /// are used to populate the log entry.</param>
    /// <param name="commandTimestamp">The timestamp indicating when the command was executed.</param>
    /// <param name="commandData">A string containing additional data associated with the command. Can be <see langword="null"/> or empty.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the unique identifier of the
    /// inserted log entry.</returns>
    public async Task<long> InsertCommandLogAsync(ICommand command, DateTime commandTimestamp, string commandData)
        => (await _dbFactory.EventSourceDb
                .Use(EventSourceDbSql.InsertCommandLog)
                .SetParameters(new InsertCommandLogSetParameter(
                    CommandId: command.CommandId,
                    StreamId: command.StreamId,
                    ActorName: $"{command.Subject.Name}",
                    CommandName: command.CommandName,
                    CommandTimestamp: $"{commandTimestamp:o}",
                    CommandData: JsonConvert.SerializeObject(command)
                ))
                .ExecuteCommandAsync())[0];

    /// <summary>
    /// Deletes an event log entry from the database based on the specified event version.
    /// </summary>
    /// <remarks>This method performs an asynchronous database operation to delete the event log entry. Ensure
    /// that the specified <paramref name="eventVersion"/> corresponds to an existing event log.</remarks>
    /// <param name="eventVersion">The version of the event log to delete. Must be a positive integer.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task DeleteEventLogAsync(long eventVersion)
        => await _dbFactory.EventSourceDb
            .Use(EventSourceDbSql.DeleteEventLog)
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
    /// Retrieves the command log associated with the specified command ID.
    /// </summary>
    /// <remarks>This method asynchronously fetches the command log from the database using the provided
    /// command ID. Ensure that the <paramref name="commandId"/> is valid and corresponds to an existing
    /// command.</remarks>
    /// <param name="commandId">The unique identifier of the command whose log is to be retrieved.</param>
    /// <returns>A <see cref="CommandLogReadModel"/> representing the command log if found; otherwise, <see langword="null"/>.</returns>
    public async Task<CommandLogReadModel?> GetCommandLogAsync(Guid commandId)
        =>  await _dbFactory.EventSourceDb
            .Use(EventSourceDbSql.GetCommandLog)
            .SetParameters(new GetCommandLog(commandId))
            .ExecuteSingleAsync<CommandLogReadModel>(MapToCommandLog);

    /// <summary>
    /// Asynchronously retrieves the unique identifier for the specified event stream.
    /// </summary>
    /// <remarks>If the event stream does not exist, it will be created and its identifier will be
    /// returned.</remarks>
    /// <param name="eventStream">The name of the event stream to retrieve the identifier for. Cannot be null or empty.</param>
    /// <returns>A <see langword="long"/> representing the unique identifier of the specified event stream.</returns>
    public async Task<long> GetEventStreamAsync(string eventStream)
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
        => await _dbFactory.EventSourceDb
            .Use(EventSourceDbSql.GetEventStreamId)
            .SetParameters(new GetEventStreamId(eventStream))
            .ExecuteSingleAsync< EventStreamIdReadModel>(MapToEventStreamId);

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

        await _dbFactory.EventSourceDb
            .Use(EventSourceDbSql.DeleteEventStreamId)
            .SetParameters(new DeleteEventStreamId(eventStream))
            .ExecuteCommandAsync();

        return await _dbFactory.EventSourceDb
            .Use(EventSourceDbSql.InsertEventStreamId)
            .SetParameters(new InsertEventStreamId(eventStream))
            .ExecuteScalarAsync(MapToLong);
    }
        
   /// <summary>
   /// Retrieves the unique identifier associated with the event name of the specified domain event type.
   /// </summary>
   /// <typeparam name="TEvent">The type of the domain event, which must implement <see cref="IEvent"/>.</typeparam>
   /// <param name="domainEvent">The domain event instance whose event name identifier is to be retrieved. Cannot be <see langword="null"/>.</param>
   /// <returns>A task that represents the asynchronous operation. The task result contains the unique identifier for the event
   /// name associated with the specified domain event type.</returns>
    internal async Task<int> GetEventNameIdFromDomainEventAsync<TEvent>(TEvent domainEvent) where TEvent : IEvent
        =>  await GetEventNameIdFromTypeAsync(domainEvent.GetType());

    /// <summary>
    /// Asynchronously retrieves the unique identifier for the event name associated with the specified event type.
    /// </summary>
    /// <typeparam name="TEvent">The type of the event, which must implement the <see cref="IEvent"/> interface.</typeparam>
    /// <returns>A task that represents the asynchronous operation. The task result contains the unique identifier  for the event
    /// name associated with the specified event type.</returns>
    public async Task<int> GetEventNameIdFromTypeAsync<TEvent>() where TEvent : IEvent
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
            await _dbFactory.EventSourceDb
                  .Use(EventSourceDbSql.DeleteEventNameId)
                  .SetParameters(new DeleteEventNameId(eventName, eventTypeName))
                  .ExecuteCommandAsync();
            var eventNameId = await _dbFactory.EventSourceDb
                  .Use(EventSourceDbSql.InsertEventNameId)
                  .SetParameters(new InsertEventNameId(eventName, eventTypeName))
                  .ExecuteScalarAsync<int>(MapToInt);
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
    public async Task<EventNameIdReadModel> GetEventNameIdFromDbAsync(string eventName)
        => await _dbFactory.EventSourceDb
            .Use(EventSourceDbSql.GetEventNameId)
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
    public async Task<long> InsertEventLogAsync(long eventStreamId, int eventNameId, string eventData, Guid commandId, DateTime eventTimestamp)
        => await _dbFactory.EventSourceDb
                .Use(EventSourceDbSql.InsertEventLog)
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
    /// <returns>A <see cref="DomainEventCollection"/> containing the domain events associated with the specified event stream
    /// ID. If no events are found, the collection will be empty.</returns>
    internal async ValueTask<ICollection<EventStreamReadModel>> GetEventStreamAsync(long eventStreamId)
        => await _dbFactory.EventSourceDb
                .Use(EventSourceDbSql.GetEventLogByEventStreamId)
                .SetParameters(new GetEventLogByEventStreamId(eventStreamId))
                .ExecuteQueryAsync<EventStreamReadModel>(MapToEventStream);

    /// <summary>
    /// Asynchronously retrieves the last N events from a specified event stream.
    /// </summary>
    /// <remarks>The method queries the event source database to fetch the specified number of recent events 
    /// from the given event stream. The events are returned in ascending order of their version.</remarks>
    /// <param name="eventStreamId">The unique identifier of the event stream from which to retrieve events.</param>
    /// <param name="lastNRange">The number of most recent events to retrieve from the event stream.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a collection of  <see
    /// cref="EventStreamReadModel"/> representing the last N events, ordered by event version.  Returns an empty
    /// collection if no events are found.</returns>
    public async ValueTask<ICollection<EventStreamReadModel>> GetEventsLastNRangeAsync(long eventStreamId, int lastNRange)
    {
        var eventLogRange = await _dbFactory.EventSourceDb
            .Use(EventSourceDbSql.GetEventLogLastNRange)
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
    /// <returns>A task that represents the asynchronous operation. The task result contains a collection of  <see
    /// cref="EventStreamReadModel"/> instances representing the events in the specified event stream.</returns>
    public async ValueTask<ICollection<EventStreamReadModel>> GetEventsFromSnapshotAsync<TSnapshot>(long eventStreamId) 
        where TSnapshot : IEvent
    {
        var snapshotEventNameId = await GetEventNameIdFromTypeAsync<TSnapshot>();
        var db = _dbFactory.EventSourceDb;
        var maxEventVersion = await db.Use(EventSourceDbSql.GetMaxEventVersion)
            .SetParameters(new GetMaxEventVersion(eventStreamId, snapshotEventNameId))
            .ExecuteScalarAsync(MapToLong);
        return maxEventVersion > 0
            ? await db.Use(EventSourceDbSql.GetEventLogByMaxEventVersion)
                .SetParameters(new GetEventLogByMaxEventVersion(eventStreamId, maxEventVersion))
                .ExecuteQueryAsync<EventStreamReadModel>(MapToEventStream)
            : await db.Use(EventSourceDbSql.GetEventLogByEventStreamId)
                .SetParameters(new GetEventLogByEventStreamId(eventStreamId))
                .ExecuteQueryAsync<EventStreamReadModel>(MapToEventStream);
    }
   
}
