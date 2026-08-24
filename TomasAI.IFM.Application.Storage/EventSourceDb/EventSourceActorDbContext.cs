using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage.CommandDeduplication;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventProjector.ReadModels;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.EventSourceDb;

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
    : ObjectDataRepository<EventSourceActorDbContext>(connectionSettings[EventSourceActorDbConnection], logger),
      IEventSourceActorDbContext,
      ICommandDuplicateGuard
{
    readonly IBlackboardService _blackboardService = IsArgumentNull.Set(blackboardService);
    readonly IDbContextFactory _dbFactory = IsArgumentNull.Set(dbFactory);
    readonly ConcurrentDictionary<string, EventNameIdReadModel> _eventNameIdCache = new();
    readonly ConcurrentDictionary<Guid, Lazy<Task<bool>>> _legacyCommandReservations = new();
    readonly Lazy<CommandDuplicateCoordinator> _commandDuplicates = new(
        static () => new CommandDuplicateCoordinator(
            CommandDuplicateCoordinator.ReadConfiguredCapacity()),
        LazyThreadSafetyMode.ExecutionAndPublication);

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
    /// Maps ordinal data from an <see cref="IObjectDataRecord"/> to a <see cref="CommandLogReadModel"/> instance.
    /// </summary>
    /// <param name="o">The object map reader containing the source data for the mapping operation.</param>
    /// <returns>A <see cref="CommandLogReadModel"/> populated with values retrieved from the specified object map reader.</returns>
    internal static CommandLogReadModel MapToCommandLog(IObjectDataRecord o)
        => new (
            CommandId: o.GetGuid(0),
            StreamId: o.GetString(1),
            AggregateName: o.GetEnum<BoundedContextName>(2),
            CommandName: o.GetString(3),
            CommandTimestamp: o.GetDateTime(4),
            CommandData: o.GetString(5)
        );

    /// <summary>
    /// Maps the provided object reader to an <see cref="EventStreamIdReadModel"/> instance.
    /// </summary>
    /// <param name="o">The object reader used to retrieve values for the <see cref="EventStreamIdReadModel"/> properties.</param>
    /// <returns>An <see cref="EventStreamIdReadModel"/> instance populated with values from the object reader.</returns>
    internal static EventStreamIdReadModel MapToEventStreamId(IObjectDataRecord o)
        => new (
            EventStreamId: o.GetLong(0),
            EventStream: o.GetString(1)
        );  

    /// <summary>
    /// Maps ordinal data from an <see cref="IObjectDataRecord"/> to an instance of <see cref="EventNameIdReadModel"/>.
    /// </summary>
    /// <param name="o">The object map reader containing the source data for the mapping operation.</param>
    /// <returns>A new <see cref="EventNameIdReadModel"/> instance populated with values retrieved from the specified object map
    /// reader.</returns>
    internal static EventNameIdReadModel MapToEventNameId(IObjectDataRecord o)
        => new(
            EventNameId: o.GetInt(0),
            EventName: o.GetString(1),
            EventTypeName: o.GetString(2)
        );

    /// <summary>
    /// Maps ordinal data from an <see cref="IObjectDataRecord"/> to an <see cref="EventLogReadModel"/> instance.
    /// </summary>
    /// <remarks>This method performs a direct mapping of properties from the object map reader to the <see
    /// cref="EventLogReadModel"/>. Ensure that the object map reader contains valid data for all required
    /// properties.</remarks>
    /// <param name="o">An object map reader containing the source data for the mapping operation.  Each property of <see
    /// cref="EventLogReadModel"/> is populated using corresponding values retrieved from this reader.</param>
    /// <returns>A new <see cref="EventLogReadModel"/> instance populated with data from the specified <see
    /// cref="IObjectDataRecord"/>.</returns>
    internal static EventLogReadModel MapToEventLog(IObjectDataRecord o)
        =>  new  (
                EventStreamId: o.GetLong(0),
                EventName: o.GetString(1),
                EventTypeName: o.GetString(2),
                EventVersion: o.GetLong(3),
                EventData: o.GetString(4),
                CommandId: o.GetGuid(5),
                EventTimestamp: o.GetString(6),
                StreamVersion: o.GetLong(7)
            );

    /// <summary>
    /// Maps durable projection state from the event-source database.
    /// </summary>
    internal static EventProjectorStateReadModel MapToEventProjectorState(
        IObjectDataRecord o)
        => new(
            eventId: o.GetLong(0),
            actorName: o.GetString(1),
            projectorName: o.GetString(2),
            isReplay: o.GetBool(3),
            attemptNumber: o.GetInt(4),
            outcome: o.GetEnum<EventProjectorOutcomeType>(5),
            stage: o.GetEnum<EventProjectorStageType>(6),
            errorMessage: o.GetString(7),
            createdTimestamp: o.GetDateTime(8),
            updatedTimestamp: o.GetDateTime(9));

    internal static EventProjectorExecutionStateReadModel MapToEventProjectorExecutionState(
        IObjectDataRecord o)
        => MapToEventProjectorExecutionState(o, 0);

    static EventProjectorExecutionStateReadModel MapToEventProjectorExecutionState(
        IObjectDataRecord o,
        int offset)
        => new(
            EventId: o.GetLong(offset),
            ActorName: o.GetString(offset + 1),
            ProjectorName: o.GetString(offset + 2),
            IsReplay: o.GetBool(offset + 3),
            AttemptNumber: o.GetInt(offset + 4),
            Outcome: o.GetEnum<EventProjectorOutcomeType>(offset + 5),
            Stage: o.GetEnum<EventProjectorStageType>(offset + 6),
            ErrorMessage: o.GetString(offset + 7),
            CreatedTimestamp: o.GetDateTime(offset + 8),
            UpdatedTimestamp: o.GetDateTime(offset + 9),
            EventStreamId: o.GetLong(offset + 10),
            SourceEventName: o.GetString(offset + 11),
            Revision: o.GetLong(offset + 12),
            ExecutionToken: o.IsNull(offset + 13) ? null : o.GetGuid(offset + 13),
            LeaseExpiresAtUtc: o.IsNull(offset + 14) ? null : o.GetDateTime(offset + 14),
            RetryCount: o.GetInt(offset + 15),
            NextAttemptAtUtc: o.IsNull(offset + 16) ? null : o.GetDateTime(offset + 16),
            LastErrorAtUtc: o.IsNull(offset + 17) ? null : o.GetDateTime(offset + 17),
            BlockedReason: o.GetString(offset + 18),
            LastCompletedStage: o.GetEnum<EventProjectorStageType>(offset + 19),
            UpdatedAtUtc: o.GetDateTime(offset + 20),
            BlockedStage: o.GetEnum<EventProjectorStageType>(offset + 21),
            StreamVersion: o.GetLong(offset + 22));

    internal static EventProjectorRecoveryItemReadModel MapToEventProjectorRecoveryItem(
        IObjectDataRecord o)
        => new(
            EventLog: new EventLogReadModel(
                EventStreamId: o.GetLong(0),
                EventName: o.GetString(1),
                EventTypeName: o.GetString(2),
                EventVersion: o.GetLong(3),
                EventData: o.GetString(4),
                CommandId: o.GetGuid(5),
                EventTimestamp: o.GetString(6),
                StreamVersion: o.GetLong(7)),
            State: MapToEventProjectorExecutionState(o, 8));

    internal static EventProjectorStreamCheckpointReadModel MapToEventProjectorStreamCheckpoint(
        IObjectDataRecord o)
        => new(
            ProjectorName: o.GetString(0),
            EventStreamId: o.GetLong(1),
            LastAppliedStreamVersion: o.GetLong(2),
            LastAppliedEventId: o.GetLong(3),
            Revision: o.GetLong(4),
            UpdatedAtUtc: o.GetDateTime(5));

    internal static EventProjectorOutboxReadModel MapToEventProjectorOutbox(IObjectDataRecord o)
        => new(
            ProjectorName: o.GetString(0),
            EventId: o.GetLong(1),
            EffectKind: o.GetEnum<EventProjectorEffectKind>(2),
            MessageId: o.GetString(3),
            EventTypeName: o.GetString(4),
            EventPayload: o.GetBytes(5),
            Status: o.GetEnum<EventProjectorOutboxStatus>(6),
            AttemptCount: o.GetInt(7),
            NextAttemptAtUtc: o.IsNull(8) ? null : o.GetDateTime(8),
            CreatedAtUtc: o.GetDateTime(9),
            PublishedAtUtc: o.IsNull(10) ? null : o.GetDateTime(10),
            LastError: o.GetString(11),
            DispatchToken: o.GetGuid(12),
            DispatchLeaseExpiresAtUtc: o.GetDateTime(13));

    internal static EventProjectorOperationalSnapshotReadModel MapToEventProjectorOperationalSnapshot(
        IObjectDataRecord o)
        => new(
            PendingCount: o.GetLong(0),
            OldestPendingAtUtc: o.IsNull(1) ? null : o.GetDateTime(1),
            BlockedCount: o.GetLong(2),
            TerminalFailedCount: o.GetLong(3),
            ExpiredLeaseCount: o.GetLong(4),
            OutboxPendingCount: o.GetLong(5),
            OldestOutboxPendingAtUtc: o.IsNull(6) ? null : o.GetDateTime(6),
            OutboxRetryCount: o.GetLong(7));

    /// <summary>
    /// Maps the specified object map reader to an <see cref="EventStreamReadModel"/> instance.
    /// </summary>
    /// <param name="o">The object map reader containing the event data to map.</param>
    /// <returns>An <see cref="EventStreamReadModel"/> populated with data from the object map reader.</returns>
    internal static EventStreamReadModel MapToEventStream(IObjectDataRecord o)
        => new()
        {
            EventTypeName = o.GetString(2),
            EventVersion = o.GetLong(3),
            EventData = o.GetString(4),
            StreamVersion = o.GetLong(7)
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
        => await GetEventStreamIdAsync(eventStream, CancellationToken.None).ConfigureAwait(false);

    public async Task<long> GetEventStreamIdAsync(string eventStream, CancellationToken cancellationToken)
    {
        var pending = _blackboardService.EventSourcing.EventStreamId.GetAsync(eventStream, InsertEntityTypeAsync);
        return (await pending.AsTask().WaitAsync(cancellationToken).ConfigureAwait(false)).EventStreamId;

        async Task<EventStreamIdReadModel> InsertEntityTypeAsync(string eventStream)
        {
            var eventStreamId = await InsertEventStreamAsync(eventStream, cancellationToken).ConfigureAwait(false);
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
    public async Task<DomainEventCollection> SaveEventsAsync(string eventStream, Guid commandId, DomainEventCollection domainEvents)
        => await SaveEventsAsync(eventStream, commandId, domainEvents, CancellationToken.None).ConfigureAwait(false);

    public async Task<DomainEventCollection> SaveEventsAsync(
        string eventStream,
        Guid commandId,
        DomainEventCollection domainEvents,
        CancellationToken cancellationToken)
    {
        var savedEvents = new DomainEventCollection();
        List<(int EventNameId, IEvent DomainEvent)> eventLogParams = [];

        var streamId = await GetEventStreamIdAsync(eventStream, cancellationToken).ConfigureAwait(false);
        foreach (var e in domainEvents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var eventNameId = await GetEventNameIdFromDomainEventAsync(e, cancellationToken).ConfigureAwait(false);
            eventLogParams.Add((eventNameId, e));
        }
        var db = _dbFactory.ActorEventSourceDb;
        var tx = db.BeginTransaction();
        try
        {
            var eventDate = DateTime.Now;
            foreach (var e in eventLogParams)
            {
                EventInitHelper.SetProperty(
                    e.DomainEvent,
                    nameof(IEvent.EventId),
                    await InsertEventLogAsync(
                        db,
                        streamId,
                        e.EventNameId,
                        e.DomainEvent.ToEventData(),
                        commandId,
                        eventDate,
                        cancellationToken).ConfigureAwait(false));
                savedEvents.Add(e.DomainEvent);
            }
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
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
    public Task InsertCommandLogAsync(ICommand command, DateTime commandTimestamp, string commandData)
        => InsertCommandLogAsync(command, commandTimestamp, commandData, CancellationToken.None);

    public Task InsertCommandLogAsync(
        ICommand command,
        DateTime commandTimestamp,
        string commandData,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandData);
        cancellationToken.ThrowIfCancellationRequested();

        var candidate = new Lazy<Task<bool>>(
            () => TryInsertCommandLogAsync(
                command,
                commandTimestamp,
                commandData,
                cancellationToken),
            LazyThreadSafetyMode.ExecutionAndPublication);
        var reservation = _legacyCommandReservations.GetOrAdd(command.CommandId, candidate);
        return ObserveLegacyReservationAsync(command.CommandId, reservation);
    }

    public async Task<bool> TryInsertCommandLogAsync(
        ICommand command,
        DateTime commandTimestamp,
        string commandData)
        => await TryInsertCommandLogAsync(
                command,
                commandTimestamp,
                commandData,
                CancellationToken.None)
            .ConfigureAwait(false);

    public async Task<bool> TryInsertCommandLogAsync(
        ICommand command,
        DateTime commandTimestamp,
        string commandData,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandData);

        return await _commandDuplicates.Value.TryAcceptAsync(
                command.CommandId,
                token => InsertCommandLogCoreAsync(command, commandTimestamp, commandData, token),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public ValueTask<bool> TryAcceptAsync(
        ICommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (_legacyCommandReservations.TryRemove(command.CommandId, out var legacyReservation))
            return AwaitLegacyReservationAsync(legacyReservation.Value, cancellationToken);

        return _commandDuplicates.Value.TryAcceptAsync(
            command.CommandId,
            token => InsertCommandLogCoreAsync(
                command,
                DateTime.UtcNow,
                JsonConvert.SerializeObject(command),
                token),
            cancellationToken);
    }

    static async ValueTask<bool> AwaitLegacyReservationAsync(
        Task<bool> reservation,
        CancellationToken cancellationToken)
        => await reservation.WaitAsync(cancellationToken).ConfigureAwait(false);

    async Task ObserveLegacyReservationAsync(
        Guid commandId,
        Lazy<Task<bool>> reservation)
    {
        try
        {
            _ = await reservation.Value.ConfigureAwait(false);
        }
        catch
        {
            _legacyCommandReservations.TryRemove(
                new KeyValuePair<Guid, Lazy<Task<bool>>>(commandId, reservation));
            throw;
        }
    }

    Task<bool> InsertCommandLogCoreAsync(
        ICommand command,
        DateTime commandTimestamp,
        string commandData,
        CancellationToken cancellationToken)
        => _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.TryInsertCommandLog)}", EventSourceDbSql.TryInsertCommandLog)
            .SetParameters(new InsertActorCommandLog(
                command.CommandId,
                command.StreamId,
                $"{command.RouteTo}",
                command.CommandName,
                $"{commandTimestamp:o}",
                $"{CommandStatus.InProgress}",
                commandData))
            .ExecuteScalarAsync(static value => value.GetBool(0), cancellationToken);

    public async Task InsertEventProjectorStateAsync(EventProjectorStateReadModel state)
        => await InsertEventProjectorStateAsync(state, CancellationToken.None).ConfigureAwait(false);

    public async Task InsertEventProjectorStateAsync(
        EventProjectorStateReadModel state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(state.ProjectorName);
        var now = DateTime.UtcNow;
        var createdTimestamp = state.CreatedTimestamp == default ? now : state.CreatedTimestamp;
        await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.UpsertEventProjectorState)}", EventSourceDbSql.UpsertEventProjectorState)
            .SetParameters(new UpsertEventProjectorState(
                eventId: state.EventId,
                actorName: state.ActorName,
                projectorName: state.ProjectorName,
                isReplay: state.IsReplay,
                attemptNumber: state.AttemptNumber,
                outcome: $"{state.Outcome}",
                stage: $"{state.Stage}",
                errorMessage: state.ErrorMessage ?? string.Empty,
                createdTimestamp: $"{createdTimestamp:o}",
                updatedTimestamp: $"{now:o}"))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets durable projection state for one event and one projector.
    /// </summary>
    public async Task<EventProjectorStateReadModel?> GetEventProjectorStateAsync(
        long eventId,
        string projectorName)
        => await GetEventProjectorStateAsync(
            eventId,
            projectorName,
            CancellationToken.None).ConfigureAwait(false);

    public async Task<EventProjectorStateReadModel?> GetEventProjectorStateAsync(
        long eventId,
        string projectorName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectorName);
        return await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventProjectorState)}", EventSourceDbSql.GetEventProjectorState)
            .SetParameters(new GetEventProjectorState(eventId, projectorName))
            .ExecuteSingleAsync<EventProjectorStateReadModel>(MapToEventProjectorState, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<EventLogReadModel?> GetEventLogByEventIdAsync(
        long eventId,
        CancellationToken cancellationToken = default)
    {
        if (eventId <= 0)
            throw new ArgumentOutOfRangeException(nameof(eventId));
        return await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventLogByEventVersion)}", EventSourceDbSql.GetEventLogByEventVersion)
            .SetParameters(new GetEventLogByEventVersion(eventId))
            .ExecuteSingleAsync<EventLogReadModel>(MapToEventLog, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<EventProjectorExecutionStateReadModel?> TryCreateEventProjectorExecutionStateAsync(
        EventProjectorExecutionStateReadModel state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        ValidateProjectorIdentity(state.EventId, state.ProjectorName);
        ArgumentException.ThrowIfNullOrWhiteSpace(state.ActorName);
        var createdTimestamp = RequireUtc(
            state.CreatedTimestamp == default ? DateTime.UtcNow : state.CreatedTimestamp,
            nameof(state.CreatedTimestamp));
        var updatedAtUtc = RequireUtc(
            state.UpdatedAtUtc == default ? createdTimestamp : state.UpdatedAtUtc,
            nameof(state.UpdatedAtUtc));

        return await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.TryCreateEventProjectorExecutionState)}", EventSourceDbSql.TryCreateEventProjectorExecutionState)
            .SetParameters(new TryCreateEventProjectorExecutionState(
                state.EventId,
                state.ActorName,
                state.ProjectorName,
                state.IsReplay,
                state.AttemptNumber,
                $"{state.Outcome}",
                $"{state.Stage}",
                state.ErrorMessage ?? string.Empty,
                $"{createdTimestamp:o}",
                $"{updatedAtUtc:o}",
                updatedAtUtc))
            .ExecuteSingleAsync<EventProjectorExecutionStateReadModel>(
                MapToEventProjectorExecutionState,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<EventProjectorExecutionStateReadModel?> GetEventProjectorExecutionStateAsync(
        long eventId,
        string projectorName,
        CancellationToken cancellationToken = default)
    {
        ValidateProjectorIdentity(eventId, projectorName);
        return await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventProjectorExecutionState)}", EventSourceDbSql.GetEventProjectorExecutionState)
            .SetParameters(new GetEventProjectorState(eventId, projectorName))
            .ExecuteSingleAsync<EventProjectorExecutionStateReadModel>(
                MapToEventProjectorExecutionState,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<EventProjectorStreamCheckpointReadModel?> GetEventProjectorStreamCheckpointAsync(
        string projectorName,
        long eventStreamId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectorName);
        if (eventStreamId <= 0)
            throw new ArgumentOutOfRangeException(nameof(eventStreamId));
        return await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventProjectorStreamCheckpoint)}", EventSourceDbSql.GetEventProjectorStreamCheckpoint)
            .SetParameters(new GetEventProjectorStreamCheckpoint(projectorName, eventStreamId))
            .ExecuteSingleAsync<EventProjectorStreamCheckpointReadModel>(
                MapToEventProjectorStreamCheckpoint,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<EventProjectorExecutionStateReadModel?> TryClaimEventProjectorExecutionAsync(
        long eventId,
        string projectorName,
        Guid executionToken,
        DateTime nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        ValidateProjectorIdentity(eventId, projectorName);
        ValidateExecutionToken(executionToken);
        nowUtc = RequireUtc(nowUtc, nameof(nowUtc));
        var leaseExpiresAtUtc = GetLeaseExpiry(nowUtc, leaseDuration);
        return await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.TryClaimEventProjectorExecution)}", EventSourceDbSql.TryClaimEventProjectorExecution)
            .SetParameters(new TryClaimEventProjectorExecution(
                eventId,
                projectorName,
                executionToken,
                leaseExpiresAtUtc,
                nowUtc,
                $"{nowUtc:o}"))
            .ExecuteSingleAsync<EventProjectorExecutionStateReadModel>(
                MapToEventProjectorExecutionState,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> HasEarlierUnresolvedEventProjectorExecutionAsync(
        long eventId,
        string projectorName,
        CancellationToken cancellationToken = default)
    {
        ValidateProjectorIdentity(eventId, projectorName);
        return await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.HasEarlierUnresolvedEventProjectorExecution)}", EventSourceDbSql.HasEarlierUnresolvedEventProjectorExecution)
            .SetParameters(new GetEventProjectorState(eventId, projectorName))
            .ExecuteScalarAsync(static value => value.GetBool(0), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<EventProjectorExecutionStateReadModel?> TryRenewEventProjectorExecutionAsync(
        long eventId,
        string projectorName,
        Guid executionToken,
        long expectedRevision,
        DateTime nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        ValidateProjectorIdentity(eventId, projectorName);
        ValidateExecutionToken(executionToken);
        if (expectedRevision < 0)
            throw new ArgumentOutOfRangeException(nameof(expectedRevision));
        nowUtc = RequireUtc(nowUtc, nameof(nowUtc));
        var leaseExpiresAtUtc = GetLeaseExpiry(nowUtc, leaseDuration);
        return await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.TryRenewEventProjectorExecution)}", EventSourceDbSql.TryRenewEventProjectorExecution)
            .SetParameters(new TryRenewEventProjectorExecution(
                eventId,
                projectorName,
                executionToken,
                expectedRevision,
                nowUtc,
                leaseExpiresAtUtc,
                $"{nowUtc:o}"))
            .ExecuteSingleAsync<EventProjectorExecutionStateReadModel>(
                MapToEventProjectorExecutionState,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<EventProjectorExecutionStateReadModel?> TryReleaseEventProjectorExecutionAsync(
        EventProjectorStateTransition transition,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        ValidateTransition(transition, terminal: false);
        if (transition.Outcome != EventProjectorOutcomeType.Retrying)
            throw new ArgumentOutOfRangeException(nameof(transition), "A released execution must be retryable.");
        if (!transition.NextAttemptAtUtc.HasValue || !transition.LastErrorAtUtc.HasValue)
            throw new ArgumentException("A released execution requires retry and error timestamps.", nameof(transition));
        nowUtc = RequireUtc(nowUtc, nameof(nowUtc));
        return await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.TryReleaseEventProjectorExecution)}", EventSourceDbSql.TryReleaseEventProjectorExecution)
            .SetParameters(new TryReleaseEventProjectorExecution(
                transition.EventId,
                transition.ProjectorName,
                transition.ExecutionToken,
                transition.ExpectedRevision,
                $"{transition.ExpectedStage}",
                nowUtc,
                transition.RetryCount,
                transition.NextAttemptAtUtc.Value,
                transition.LastErrorAtUtc.Value,
                transition.ErrorMessage ?? string.Empty,
                nowUtc,
                $"{nowUtc:o}"))
            .ExecuteSingleAsync<EventProjectorExecutionStateReadModel>(
                MapToEventProjectorExecutionState,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<EventProjectorExecutionStateReadModel?> TryTransitionEventProjectorExecutionAsync(
        EventProjectorStateTransition transition,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        ValidateTransition(transition, terminal: false);
        nowUtc = RequireUtc(nowUtc, nameof(nowUtc));
        return await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.TryTransitionEventProjectorExecution)}", EventSourceDbSql.TryTransitionEventProjectorExecution)
            .SetParameters(new TryTransitionEventProjectorExecution(
                transition.EventId,
                transition.ProjectorName,
                transition.ExecutionToken,
                transition.ExpectedRevision,
                $"{transition.ExpectedStage}",
                nowUtc,
                $"{transition.NextStage}",
                $"{transition.Outcome}",
                $"{transition.LastCompletedStage}",
                transition.RetryCount,
                ValidateOptionalUtc(transition.NextAttemptAtUtc, nameof(transition.NextAttemptAtUtc)),
                ValidateOptionalUtc(transition.LastErrorAtUtc, nameof(transition.LastErrorAtUtc)),
                transition.ErrorMessage ?? string.Empty,
                transition.BlockedReason ?? string.Empty,
                nowUtc,
                $"{nowUtc:o}"))
            .ExecuteSingleAsync<EventProjectorExecutionStateReadModel>(
                MapToEventProjectorExecutionState,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<EventProjectorExecutionStateReadModel?> TryTerminalizeEventProjectorExecutionAsync(
        EventProjectorStateTransition transition,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        ValidateTransition(transition, terminal: true);
        nowUtc = RequireUtc(nowUtc, nameof(nowUtc));
        return await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.TryTerminalizeEventProjectorExecution)}", EventSourceDbSql.TryTerminalizeEventProjectorExecution)
            .SetParameters(new TryTerminalizeEventProjectorExecution(
                transition.EventId,
                transition.ProjectorName,
                transition.ExecutionToken,
                transition.ExpectedRevision,
                $"{transition.ExpectedStage}",
                nowUtc,
                $"{transition.Outcome}",
                $"{transition.LastCompletedStage}",
                transition.RetryCount,
                ValidateOptionalUtc(transition.LastErrorAtUtc, nameof(transition.LastErrorAtUtc)),
                transition.ErrorMessage ?? string.Empty,
                transition.BlockedReason ?? string.Empty,
                nowUtc,
                $"{nowUtc:o}"))
            .ExecuteSingleAsync<EventProjectorExecutionStateReadModel>(
                MapToEventProjectorExecutionState,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<EventProjectorExecutionStateReadModel?> TryTransitionEventProjectorExecutionWithOutboxAsync(
        EventProjectorStateTransition transition,
        EventProjectorOutboxMessage message,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        ValidateTransition(transition, terminal: false);
        ValidateOutboxMessage(transition, message);
        nowUtc = RequireUtc(nowUtc, nameof(nowUtc));
        var parameters = new TryTransitionEventProjectorExecution(
            transition.EventId,
            transition.ProjectorName,
            transition.ExecutionToken,
            transition.ExpectedRevision,
            $"{transition.ExpectedStage}",
            nowUtc,
            $"{transition.NextStage}",
            $"{transition.Outcome}",
            $"{transition.LastCompletedStage}",
            transition.RetryCount,
            ValidateOptionalUtc(transition.NextAttemptAtUtc, nameof(transition.NextAttemptAtUtc)),
            ValidateOptionalUtc(transition.LastErrorAtUtc, nameof(transition.LastErrorAtUtc)),
            transition.ErrorMessage ?? string.Empty,
            transition.BlockedReason ?? string.Empty,
            nowUtc,
            $"{nowUtc:o}");
        return await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.TryTransitionEventProjectorExecutionWithOutbox)}", EventSourceDbSql.TryTransitionEventProjectorExecutionWithOutbox)
            .SetParameters(new TryTransitionEventProjectorExecutionWithOutbox(
                parameters,
                $"{message.Identity.EffectKind}",
                message.MessageId,
                message.EventTypeName,
                message.EventPayload,
                nowUtc))
            .ExecuteSingleAsync<EventProjectorExecutionStateReadModel>(MapToEventProjectorExecutionState, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<EventProjectorExecutionStateReadModel?> TryTerminalizeEventProjectorExecutionWithOutboxAsync(
        EventProjectorStateTransition transition,
        EventProjectorOutboxMessage message,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        ValidateTransition(transition, terminal: true);
        ValidateOutboxMessage(transition, message);
        nowUtc = RequireUtc(nowUtc, nameof(nowUtc));
        var parameters = new TryTerminalizeEventProjectorExecution(
            transition.EventId,
            transition.ProjectorName,
            transition.ExecutionToken,
            transition.ExpectedRevision,
            $"{transition.ExpectedStage}",
            nowUtc,
            $"{transition.Outcome}",
            $"{transition.LastCompletedStage}",
            transition.RetryCount,
            ValidateOptionalUtc(transition.LastErrorAtUtc, nameof(transition.LastErrorAtUtc)),
            transition.ErrorMessage ?? string.Empty,
            transition.BlockedReason ?? string.Empty,
            nowUtc,
            $"{nowUtc:o}");
        return await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.TryTerminalizeEventProjectorExecutionWithOutbox)}", EventSourceDbSql.TryTerminalizeEventProjectorExecutionWithOutbox)
            .SetParameters(new TryTerminalizeEventProjectorExecutionWithOutbox(
                parameters,
                $"{message.Identity.EffectKind}",
                message.MessageId,
                message.EventTypeName,
                message.EventPayload,
                nowUtc))
            .ExecuteSingleAsync<EventProjectorExecutionStateReadModel>(MapToEventProjectorExecutionState, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<EventProjectorOutboxReadModel>> ClaimEventProjectorOutboxAsync(
        string projectorName,
        Guid dispatchToken,
        DateTime nowUtc,
        TimeSpan leaseDuration,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectorName);
        ValidateExecutionToken(dispatchToken);
        if (batchSize is < 1 or > 2_048)
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        nowUtc = RequireUtc(nowUtc, nameof(nowUtc));
        var leaseExpiresAtUtc = GetLeaseExpiry(nowUtc, leaseDuration);
        return [.. await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.ClaimEventProjectorOutbox)}", EventSourceDbSql.ClaimEventProjectorOutbox)
            .SetParameters(new ClaimEventProjectorOutbox(
                projectorName,
                dispatchToken,
                leaseExpiresAtUtc,
                nowUtc,
                batchSize))
            .ExecuteQueryAsync<EventProjectorOutboxReadModel>(MapToEventProjectorOutbox, cancellationToken)
            .ConfigureAwait(false)];
    }

    public async Task<bool> MarkEventProjectorOutboxPublishedAsync(
        EventProjectorOutboxReadModel message,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        nowUtc = RequireUtc(nowUtc, nameof(nowUtc));
        var affected = await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.MarkEventProjectorOutboxPublished)}", EventSourceDbSql.MarkEventProjectorOutboxPublished)
            .SetParameters(new MarkEventProjectorOutboxPublished(
                message.ProjectorName,
                message.EventId,
                $"{message.EffectKind}",
                message.DispatchToken,
                nowUtc,
                nowUtc))
            .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
        return affected.Sum() == 1;
    }

    public async Task<bool> ReleaseEventProjectorOutboxAsync(
        EventProjectorOutboxReadModel message,
        EventProjectorOutboxStatus status,
        DateTime? nextAttemptAtUtc,
        string lastError,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (status is not EventProjectorOutboxStatus.Retrying and not EventProjectorOutboxStatus.Failed)
            throw new ArgumentOutOfRangeException(nameof(status));
        nowUtc = RequireUtc(nowUtc, nameof(nowUtc));
        nextAttemptAtUtc = ValidateOptionalUtc(nextAttemptAtUtc, nameof(nextAttemptAtUtc));
        var affected = await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.ReleaseEventProjectorOutbox)}", EventSourceDbSql.ReleaseEventProjectorOutbox)
            .SetParameters(new ReleaseEventProjectorOutbox(
                message.ProjectorName,
                message.EventId,
                $"{message.EffectKind}",
                message.DispatchToken,
                nowUtc,
                $"{status}",
                nextAttemptAtUtc,
                lastError ?? string.Empty))
            .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
        return affected.Sum() == 1;
    }

    public async Task<IReadOnlyList<EventProjectorExecutionStateReadModel>> GetEventProjectorOperationalStatePageAsync(
        string projectorName,
        EventProjectorOperationalStatus status,
        long afterEventId,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectorName);
        if (!Enum.IsDefined(status))
            throw new ArgumentOutOfRangeException(nameof(status));
        if (afterEventId < 0)
            throw new ArgumentOutOfRangeException(nameof(afterEventId));
        if (batchSize is < 1 or > 2_048)
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        return [.. await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventProjectorOperationalStatePage)}", EventSourceDbSql.GetEventProjectorOperationalStatePage)
            .SetParameters(new GetEventProjectorOperationalStatePage(
                projectorName, $"{status}", afterEventId, batchSize))
            .ExecuteQueryAsync<EventProjectorExecutionStateReadModel>(
                MapToEventProjectorExecutionState, cancellationToken)
            .ConfigureAwait(false)];
    }

    public async Task<EventProjectorOperationalSnapshotReadModel> GetEventProjectorOperationalSnapshotAsync(
        string projectorName,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectorName);
        nowUtc = RequireUtc(nowUtc, nameof(nowUtc));
        return await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventProjectorOperationalSnapshot)}", EventSourceDbSql.GetEventProjectorOperationalSnapshot)
            .SetParameters(new GetEventProjectorOperationalSnapshot(projectorName, nowUtc))
            .ExecuteSingleAsync<EventProjectorOperationalSnapshotReadModel>(
                MapToEventProjectorOperationalSnapshot,
                cancellationToken)
            .ConfigureAwait(false)
            ?? new EventProjectorOperationalSnapshotReadModel(0, null, 0, 0, 0, 0, null, 0);
    }

    public async Task<EventProjectorExecutionStateReadModel?> TryRetryEventProjectorExecutionAsync(
        long eventId,
        string projectorName,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        ValidateProjectorIdentity(eventId, projectorName);
        nowUtc = RequireUtc(nowUtc, nameof(nowUtc));
        return await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.TryRetryEventProjectorExecution)}", EventSourceDbSql.TryRetryEventProjectorExecution)
            .SetParameters(new RetryEventProjectorExecution(
                eventId, projectorName, nowUtc, $"{nowUtc:o}"))
            .ExecuteSingleAsync<EventProjectorExecutionStateReadModel>(
                MapToEventProjectorExecutionState, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<EventProjectorExecutionStateReadModel?> TrySkipEventProjectorExecutionAsync(
        long eventId,
        string projectorName,
        string reason,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        ValidateProjectorIdentity(eventId, projectorName);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (reason.Length > 2_000)
            throw new ArgumentOutOfRangeException(nameof(reason));
        nowUtc = RequireUtc(nowUtc, nameof(nowUtc));
        return await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.TrySkipEventProjectorExecution)}", EventSourceDbSql.TrySkipEventProjectorExecution)
            .SetParameters(new SkipEventProjectorExecution(
                eventId, projectorName, reason, nowUtc, $"{nowUtc:o}"))
            .ExecuteSingleAsync<EventProjectorExecutionStateReadModel>(
                MapToEventProjectorExecutionState, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<EventProjectorRecoveryItemReadModel>> GetEventProjectorRecoveryPageAsync(
        string projectorName,
        IReadOnlyCollection<string> eventNames,
        long afterEventId,
        DateTime nowUtc,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectorName);
        ArgumentNullException.ThrowIfNull(eventNames);
        if (eventNames.Count == 0)
            return [];
        if (afterEventId < 0)
            throw new ArgumentOutOfRangeException(nameof(afterEventId));
        if (batchSize is < 1 or > 2_048)
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        nowUtc = RequireUtc(nowUtc, nameof(nowUtc));

        return [.. await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventProjectorRecoveryPage)}", EventSourceDbSql.GetEventProjectorRecoveryPage)
            .SetParameters(new GetEventProjectorRecoveryPage(
                projectorName,
                string.Join(',', eventNames),
                afterEventId,
                nowUtc,
                batchSize))
            .ExecuteQueryAsync<EventProjectorRecoveryItemReadModel>(
                MapToEventProjectorRecoveryItem,
                cancellationToken)
            .ConfigureAwait(false)];
    }

    /// <summary>
    /// Gets event-log entries that the named projector supports and has not terminally processed.
    /// </summary>
    public async Task<ICollection<EventLogReadModel>> GetUncompletedEventProjectorEventsAsync(
        string projectorName,
        IReadOnlyCollection<string> eventNames)
        => await GetUncompletedEventProjectorEventsAsync(
            projectorName,
            eventNames,
            CancellationToken.None).ConfigureAwait(false);

    public async Task<ICollection<EventLogReadModel>> GetUncompletedEventProjectorEventsAsync(
        string projectorName,
        IReadOnlyCollection<string> eventNames,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectorName);
        ArgumentNullException.ThrowIfNull(eventNames);
        if (eventNames.Count == 0)
            return [];

        return await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetUncompletedEventProjectorEvents)}", EventSourceDbSql.GetUncompletedEventProjectorEvents)
            .SetParameters(new GetUncompletedEventProjectorEvents(
                projectorName,
                string.Join(',', eventNames)))
            .ExecuteQueryAsync<EventLogReadModel>(MapToEventLog, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously updates the log entry for a specified command with a new status and timestamp.
    /// </summary>
    /// <param name="commandId">The unique identifier of the command whose log entry is to be updated.</param>
    /// <param name="updateTimestamp">The date and time to record as the update timestamp for the command log entry.</param>
    /// <param name="commandStatus">The new status to assign to the command in the log.</param>
    /// <returns>A task that represents the asynchronous update operation.</returns>
    public async Task UpdateCommandLogAsync(Guid commandId, DateTime updateTimestamp, CommandStatus commandStatus)
        => await UpdateCommandLogAsync(commandId, updateTimestamp, commandStatus, CancellationToken.None).ConfigureAwait(false);

    public async Task UpdateCommandLogAsync(
        Guid commandId,
        DateTime updateTimestamp,
        CommandStatus commandStatus,
        CancellationToken cancellationToken)
        => await _dbFactory.ActorEventSourceDb
                .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.UpdateCommandLog)}", EventSourceDbSql.UpdateCommandLog)
                .SetParameters(new UpdateCommandLog(
                    commandId: commandId,
                    commandStatus: $"{commandStatus}",
                    updateTimestamp: updateTimestamp
                ))
                .ExecuteCommandAsync(cancellationToken);

    /// <summary>
    /// Deletes an event log entry from the database based on the specified event version.
    /// </summary>
    /// <remarks>This method performs an asynchronous database operation to delete the event log entry. Ensure
    /// that the specified <paramref name="eventVersion"/> corresponds to an existing event log.</remarks>
    /// <param name="eventVersion">The version of the event log to delete. Must be a positive integer.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task DeleteEventLogAsync(long eventVersion)
        => await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.DeleteEventLog)}", EventSourceDbSql.DeleteEventLog)
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
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.DeleteEventLogByStreamId)}", EventSourceDbSql.DeleteEventLogByStreamId)
            .SetParameters(new DeleteEventLogByStreamId(streamId))
            .ExecuteCommandAsync();

    /// <summary>
    /// Asynchronously deletes the event stream with the specified identifier from the data store.
    /// </summary>
    /// <param name="eventStreamId">The unique identifier of the event stream to delete.</param>
    /// <returns>A task that represents the asynchronous delete operation.</returns>
    public async Task DeleteEventStreamByIdAsync(long eventStreamId)
        => await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.DeleteEventStreamById)}", EventSourceDbSql.DeleteEventStreamById)
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
    public async Task<CommandLogReadModel?> GetCommandLogAsync(Guid commandId)
        =>  await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetCommandLog)}", EventSourceDbSql.GetCommandLog)
            .SetParameters(new GetCommandLog(commandId))
            .ExecuteSingleAsync<CommandLogReadModel>(MapToCommandLog);

    public async Task<bool> HasEventForCommandAsync(Guid commandId)
        => await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.HasEventForCommand)}", EventSourceDbSql.HasEventForCommand)
            .SetParameters(new GetCommandLog(commandId))
            .ExecuteScalarAsync(static value => value.GetBool(0));

    /// <summary>
    /// Asynchronously retrieves the unique identifier for the specified event stream.
    /// </summary>
    /// <remarks>If the event stream does not exist, it will be created and its identifier will be
    /// returned.</remarks>
    /// <param name="eventStream">The name of the event stream to retrieve the identifier for. Cannot be null or empty.</param>
    /// <returns>A <see langword="long"/> representing the unique identifier of the specified event stream.</returns>
    internal async Task<long> GetEventStreamAsync(string eventStream)
    {
        return (await _blackboardService.EventSourcing.EventStreamId.GetAsync(eventStream, InsertEventStream)).EventStreamId;

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
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventStreamId)}", EventSourceDbSql.GetEventStreamId)
            .SetParameters(new GetEventStreamId(eventStream))
            .ExecuteSingleAsync<EventStreamIdReadModel>(MapToEventStreamId);

    /// <summary>
    /// Inserts an event stream into the database if it does not already exist.
    /// </summary>
    /// <remarks>The atomic database upsert makes concurrent first use of the same stream safe.</remarks>
    /// <param name="eventStream">The name of the event stream to insert. This value cannot be null or empty.</param>
    /// <returns>The unique identifier of the event stream. If the event stream already exists, its existing identifier is
    /// returned; otherwise, the identifier of the newly inserted event stream is returned.</returns>
    internal async Task<long> InsertEventStreamAsync(string eventStream)
        => await InsertEventStreamAsync(eventStream, CancellationToken.None).ConfigureAwait(false);

    internal async Task<long> InsertEventStreamAsync(string eventStream, CancellationToken cancellationToken)
    {
        var eventStreamIdModel = await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventStreamId)}", EventSourceDbSql.GetEventStreamId)
            .SetParameters(new GetEventStreamId(eventStream))
            .ExecuteSingleAsync<EventStreamIdReadModel>(MapToEventStreamId, cancellationToken)
            .ConfigureAwait(false);
        if (eventStreamIdModel is not null)
            return eventStreamIdModel.EventStreamId;

        return await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.InsertEventStreamId)}", EventSourceDbSql.InsertEventStreamId)
            .SetParameters(new InsertEventStreamId(eventStream))
            .ExecuteScalarAsync(MapToLong, cancellationToken);
    }
        
   /// <summary>
   /// Retrieves the unique identifier associated with the event name of the specified domain event type.
   /// </summary>
   /// <typeparam name="TEvent">The type of the domain event, which must implement <see cref="IEvent"/>.</typeparam>
   /// <param name="domainEvent">The domain event instance whose event name identifier is to be retrieved. Cannot be <see langword="null"/>.</param>
   /// <returns>A task that represents the asynchronous operation. The task result contains the unique identifier for the event
   /// name associated with the specified domain event type.</returns>
    public async Task<int> GetEventNameIdFromDomainEventAsync<TEvent>(TEvent domainEvent) where TEvent : IEvent
        => await GetEventNameIdFromDomainEventAsync(domainEvent, CancellationToken.None).ConfigureAwait(false);

    public async Task<int> GetEventNameIdFromDomainEventAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken) where TEvent : IEvent
        => await GetEventNameIdFromTypeAsync(domainEvent.GetType(), cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Asynchronously retrieves the unique identifier for the event name associated with the specified event type.
    /// </summary>
    /// <typeparam name="TEvent">The type of the event, which must implement the <see cref="IEvent"/> interface.</typeparam>
    /// <returns>A task that represents the asynchronous operation. The task result contains the unique identifier  for the event
    /// name associated with the specified event type.</returns>
    internal async Task<int> GetEventNameIdFromTypeAsync<TEvent>() where TEvent : IEvent
        => await GetEventNameIdFromTypeAsync(typeof(TEvent), CancellationToken.None).ConfigureAwait(false);

    internal async Task<int> GetEventNameIdFromTypeAsync<TEvent>(CancellationToken cancellationToken) where TEvent : IEvent
        => await GetEventNameIdFromTypeAsync(typeof(TEvent), cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Asynchronously retrieves the unique identifier for an event name based on the specified event type.
    /// </summary>
    /// <remarks>This method interacts with a data source to retrieve or insert the event name identifier. If
    /// the event name  identifier does not exist, it will be created and stored in the database.</remarks>
    /// <param name="eventType">The <see cref="Type"/> of the event for which the identifier is to be retrieved.  The <see
    /// cref="Type.FullName"/> and <see cref="Type.Name"/> properties are used to determine the event name and type.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the unique identifier  for the event
    /// name associated with the specified event type.</returns>
    async Task<int> GetEventNameIdFromTypeAsync(Type eventType, CancellationToken cancellationToken)
    {
        //var eventTypeFullName = string.IsNullOrEmpty(eventType.AssemblyQualifiedName) ? string.Empty : $"{AssemblyQualifiedName}";
        var eventTypeFullName = eventType.AssemblyQualifiedName;
        if(!_eventNameIdCache.TryGetValue(eventTypeFullName, out EventNameIdReadModel eventNameIdModel))
        {
            eventNameIdModel = await GetEventNameIdFromDbAsync(eventType.Name, eventTypeFullName, cancellationToken).ConfigureAwait(false);
            if (eventNameIdModel.IsValid)
            {
                _eventNameIdCache.TryAdd(eventTypeFullName, eventNameIdModel);
                return eventNameIdModel.EventNameId;
            }
            var newEventNameIdModel = await InsertEventNameIdAsync(eventType.Name, eventTypeFullName).ConfigureAwait(false);
            _eventNameIdCache.TryAdd(eventTypeFullName, newEventNameIdModel);
            return newEventNameIdModel.EventNameId;
        }
        return eventNameIdModel.EventNameId;
        //return (await _blackboardService.EventSourcing.EventNameId.GetAsync(eventType.Name, eventTypeFullName, InsertEventNameIdAsync)).EventNameId;

        async Task<EventNameIdReadModel> InsertEventNameIdAsync(string eventName, string eventTypeName)
        {
            var eventNameIdModel = await GetEventNameIdFromDbAsync(eventName, eventTypeName, cancellationToken).ConfigureAwait(false);
            if (eventNameIdModel.IsValid)
                return eventNameIdModel;
            var eventNameId = await _dbFactory.ActorEventSourceDb
                  .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.InsertEventNameId)}", EventSourceDbSql.InsertEventNameId)
                  .SetParameters(new InsertEventNameId(eventName, eventTypeName))
                  .ExecuteScalarAsync(MapToInt, cancellationToken);
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
    /// <param name="eventTypeName">The persisted type identity paired with the event name.</param>
    /// <returns>An <see cref="EventNameIdReadModel"/> containing the event name and ID if the event is found; otherwise, <see
    /// langword="null"/>.</returns>
    internal async Task<EventNameIdReadModel> GetEventNameIdFromDbAsync(string eventName, string eventTypeName)
        => await GetEventNameIdFromDbAsync(eventName, eventTypeName, CancellationToken.None).ConfigureAwait(false);

    internal async Task<EventNameIdReadModel> GetEventNameIdFromDbAsync(
        string eventName,
        string eventTypeName,
        CancellationToken cancellationToken)
        => await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventNameId)}", EventSourceDbSql.GetEventNameId)
            .SetParameters(new GetEventNameId(eventName, eventTypeName))
            .ExecuteSingleAsync<EventNameIdReadModel>(MapToEventNameId, cancellationToken);

    /// <summary>
    /// Inserts a new event log entry into the database asynchronously.
    /// </summary>
    /// <remarks>This method performs an asynchronous database operation to insert an event log entry. Ensure
    /// that the provided parameters are valid and consistent with the database schema.</remarks>
    /// <param name="db">The actor event-source repository that owns the surrounding transaction.</param>
    /// <param name="eventStreamId">The unique identifier of the event stream to which the event belongs.</param>
    /// <param name="eventNameId">The identifier of the event name, representing the type or category of the event.</param>
    /// <param name="eventData">The serialized data associated with the event. This cannot be null or empty.</param>
    /// <param name="commandId">The unique identifier of the command that triggered the event.</param>
    /// <param name="eventTimestamp">The timestamp of the event, in UTC, formatted as an ISO 8601 string.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the unique identifier of the newly
    /// inserted event log entry.</returns>
    static async Task<long> InsertEventLogAsync(
        IObjectRepository<EventSourceActorDbContext> db,
        long eventStreamId,
        int eventNameId,
        string eventData,
        Guid commandId,
        DateTime eventTimestamp,
        CancellationToken cancellationToken)
        => await db
                .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.InsertEventLog)}", EventSourceDbSql.InsertEventLog)
                .SetParameters(new InsertEventLog(eventStreamId, eventNameId, eventData, commandId, $"{eventTimestamp:o}"))
                .ExecuteScalarAsync(MapToLong, cancellationToken);

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
                .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventLogByEventStreamId)}", EventSourceDbSql.GetEventLogByEventStreamId)
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
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventLogLastNRange)}", EventSourceDbSql.GetEventLogLastNRange)
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
        var maxEventVersion = await db.Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetMaxEventVersion)}", EventSourceDbSql.GetMaxEventVersion)
            .SetParameters(new GetMaxEventVersion(eventStreamId, snapshotEventNameId))
            .ExecuteScalarAsync(MapToLong);
        return maxEventVersion > 0
            ? await db.Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventLogByMaxEventVersion)}", EventSourceDbSql.GetEventLogByMaxEventVersion)
                .SetParameters(new GetEventLogByMaxEventVersion(eventStreamId, maxEventVersion))
                .ExecuteQueryAsync<EventStreamReadModel>(MapToEventStream)
            : await db.Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventLogByEventStreamId)}", EventSourceDbSql.GetEventLogByEventStreamId)
                .SetParameters(new GetEventLogByEventStreamId(eventStreamId))
                .ExecuteQueryAsync<EventStreamReadModel>(MapToEventStream);
    }

    /// <summary>
    /// Processes the full event stream by mapping records and invoking the provided reducer action.
    /// </summary>
    /// <typeparam name="TState">The actor state type that implements <see cref="IActorState{TState}"/>.</typeparam>
    /// <param name="eventStreamId">The unique identifier of the event stream to process.</param>
    /// <param name="reducerAction">The action invoked with the mapped <see cref="EventStreamReadModel"/> sequence.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    public async ValueTask MapReduceActorEventStreamAsync<TState>(long eventStreamId, Action<IEnumerable<EventStreamReadModel>> reducerAction) where TState : IActorState<TState>
        => await MapReduceActorEventStreamAsync<TState>(eventStreamId, reducerAction, CancellationToken.None).ConfigureAwait(false);

    public async ValueTask MapReduceActorEventStreamAsync<TState>(
        long eventStreamId,
        Action<IEnumerable<EventStreamReadModel>> reducerAction,
        CancellationToken cancellationToken)
        where TState : IActorState<TState>
    {
        var eventStream = new EventStreamReadModel();
        await _dbFactory.ActorEventSourceDb
                .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventLogByEventStreamId)}", EventSourceDbSql.GetEventLogByEventStreamId)
                .SetParameters(new GetEventLogByEventStreamId(eventStreamId))
                .ExecuteMapReduceAsync(EventStreamMapper, reducerAction, cancellationToken);

        EventStreamReadModel EventStreamMapper(IObjectDataRecord o)
        {
            eventStream.EventVersion = o.GetLong(3);
            eventStream.EventTypeName = o.GetString(2);
            eventStream.EventData = o.GetString(4);
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
        => await MapReduceActorEventStreamAsync<TState, TEvent>(eventStreamId, lastNRange, reducerAction, CancellationToken.None).ConfigureAwait(false);

    public async ValueTask MapReduceActorEventStreamAsync<TState, TEvent>(
        long eventStreamId,
        int lastNRange,
        Action<IEnumerable<EventStreamReadModel>> reducerAction,
        CancellationToken cancellationToken)
        where TState : IActorState<TState>
        where TEvent : IEvent
    {
        var eventNameId = await GetEventNameIdFromTypeAsync<TEvent>(cancellationToken).ConfigureAwait(false);
        await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventLogLastNRangeByEventName)}", EventSourceDbSql.GetEventLogLastNRangeByEventName)
            .SetParameters(new GetEventLogLastNRangeByEventName(
                eventStreamId,
                eventNameId,
                Math.Max(0, lastNRange)))
            .ExecuteMapReduceAsync(MapToEventStream, reducerAction, cancellationToken);
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
        => await MapReduceActorEventStreamAsync<TState, TSnapshot>(eventStreamId, reducerAction, CancellationToken.None).ConfigureAwait(false);

    public async ValueTask MapReduceActorEventStreamAsync<TState, TSnapshot>(
        long eventStreamId,
        Action<IEnumerable<EventStreamReadModel>> reducerAction,
        CancellationToken cancellationToken)
        where TState : IActorState<TState>
        where TSnapshot : IEvent
    {
        var eventStream = new EventStreamReadModel();
        var snapshotEventNameId = await GetEventNameIdFromTypeAsync<TSnapshot>(cancellationToken).ConfigureAwait(false);
        var db = _dbFactory.ActorEventSourceDb;
        var maxEventVersion = await db.Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetMaxEventVersion)}", EventSourceDbSql.GetMaxEventVersion)
            .SetParameters(new GetMaxEventVersion(eventStreamId, snapshotEventNameId))
            .ExecuteScalarAsync(MapToLong, cancellationToken);
        if (maxEventVersion > 0)
            await db.Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventLogByMaxEventVersion)}", EventSourceDbSql.GetEventLogByMaxEventVersion)
                .SetParameters(new GetEventLogByMaxEventVersion(eventStreamId, maxEventVersion))
                .ExecuteMapReduceAsync(EventStreamMapper, reducerAction, cancellationToken);
        else
            await db.Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventLogByEventStreamId)}", EventSourceDbSql.GetEventLogByEventStreamId)
                .SetParameters(new GetEventLogByEventStreamId(eventStreamId))
                .ExecuteMapReduceAsync(EventStreamMapper, reducerAction, cancellationToken);

        EventStreamReadModel EventStreamMapper(IObjectDataRecord o)
        {
            eventStream.EventVersion = o.GetLong(3);
            eventStream.EventTypeName = o.GetString(2);
            eventStream.EventData = o.GetString(4);
            return eventStream;
        }
    }

    /// <summary>
    /// Replays the latest snapshot followed by the last N matching post-snapshot events.
    /// A missing snapshot produces an empty sequence. Results are streamed in ascending event-version order.
    /// </summary>
    public async ValueTask MapReduceActorEventStreamFromSnapshotLastNRangeAsync<TState, TSnapshot, TRangeEvent>(
        long eventStreamId,
        int lastNRange,
        Action<IEnumerable<EventStreamReadModel>> reducerAction)
        where TState : IActorState<TState>
        where TSnapshot : IEvent
        where TRangeEvent : IEvent
        => await MapReduceActorEventStreamFromSnapshotLastNRangeAsync<TState, TSnapshot, TRangeEvent>(
            eventStreamId,
            lastNRange,
            reducerAction,
            CancellationToken.None).ConfigureAwait(false);

    public async ValueTask MapReduceActorEventStreamFromSnapshotLastNRangeAsync<TState, TSnapshot, TRangeEvent>(
        long eventStreamId,
        int lastNRange,
        Action<IEnumerable<EventStreamReadModel>> reducerAction,
        CancellationToken cancellationToken)
        where TState : IActorState<TState>
        where TSnapshot : IEvent
        where TRangeEvent : IEvent
    {
        var snapshotEventNameId = await GetEventNameIdFromTypeAsync<TSnapshot>(cancellationToken).ConfigureAwait(false);
        var rangeEventNameId = await GetEventNameIdFromTypeAsync<TRangeEvent>(cancellationToken).ConfigureAwait(false);
        await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventLogFromSnapshotLastNRange)}", EventSourceDbSql.GetEventLogFromSnapshotLastNRange)
            .SetParameters(new GetEventLogFromSnapshotLastNRange(
                eventStreamId,
                snapshotEventNameId,
                rangeEventNameId,
                Math.Max(0, lastNRange)))
            .ExecuteMapReduceAsync(MapToEventStream, reducerAction, cancellationToken);
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
    {
        var eventNameId = await GetEventNameIdFromTypeAsync<TEvent>();
        return await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventLogLastNRangeByEventName)}", EventSourceDbSql.GetEventLogLastNRangeByEventName)
            .SetParameters(new GetEventLogLastNRangeByEventName(
                eventStreamId,
                eventNameId,
                Math.Max(0, lastNRange)))
            .ExecuteQueryAsync<EventStreamReadModel>(MapToEventStream);
    }

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

    static void ValidateProjectorIdentity(long eventId, string projectorName)
    {
        if (eventId <= 0)
            throw new ArgumentOutOfRangeException(nameof(eventId), eventId, "The event id must be positive.");
        ArgumentException.ThrowIfNullOrWhiteSpace(projectorName);
    }

    static void ValidateExecutionToken(Guid executionToken)
    {
        if (executionToken == Guid.Empty)
            throw new ArgumentException("The execution token cannot be empty.", nameof(executionToken));
    }

    static DateTime RequireUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
            throw new ArgumentException("The timestamp must use DateTimeKind.Utc.", parameterName);
        return value;
    }

    static DateTime? ValidateOptionalUtc(DateTime? value, string parameterName)
        => value.HasValue ? RequireUtc(value.Value, parameterName) : null;

    static DateTime GetLeaseExpiry(DateTime nowUtc, TimeSpan leaseDuration)
    {
        RequireUtc(nowUtc, nameof(nowUtc));
        if (leaseDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(leaseDuration), leaseDuration, "The lease duration must be positive.");
        return nowUtc.Add(leaseDuration);
    }

    static void ValidateTransition(EventProjectorStateTransition transition, bool terminal)
    {
        ArgumentNullException.ThrowIfNull(transition);
        ValidateProjectorIdentity(transition.EventId, transition.ProjectorName);
        ValidateExecutionToken(transition.ExecutionToken);
        if (transition.ExpectedRevision < 0)
            throw new ArgumentOutOfRangeException(nameof(transition.ExpectedRevision));
        if (transition.ExpectedStage == EventProjectorStageType.Completed)
            throw new ArgumentOutOfRangeException(nameof(transition.ExpectedStage), "A completed execution cannot transition again.");
        if (transition.RetryCount < 0)
            throw new ArgumentOutOfRangeException(nameof(transition.RetryCount));

        ValidateOptionalUtc(transition.NextAttemptAtUtc, nameof(transition.NextAttemptAtUtc));
        ValidateOptionalUtc(transition.LastErrorAtUtc, nameof(transition.LastErrorAtUtc));

        if (terminal)
        {
            if (transition.Outcome is EventProjectorOutcomeType.Processing or EventProjectorOutcomeType.Retrying)
                throw new ArgumentOutOfRangeException(nameof(transition.Outcome), "A terminal transition requires a terminal outcome.");
        }
        else
        {
            if (transition.NextStage == EventProjectorStageType.Completed)
                throw new ArgumentOutOfRangeException(nameof(transition.NextStage), "Use terminalization to complete an execution.");
            if (transition.Outcome is not (EventProjectorOutcomeType.Processing or EventProjectorOutcomeType.Retrying))
                throw new ArgumentOutOfRangeException(nameof(transition.Outcome), "A non-terminal transition requires an active outcome.");
        }
    }

    static void ValidateOutboxMessage(
        EventProjectorStateTransition transition,
        EventProjectorOutboxMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.Identity.EventId != transition.EventId
            || !string.Equals(message.Identity.ProjectorName, transition.ProjectorName, StringComparison.Ordinal))
            throw new ArgumentException("The outbox identity must match the transitioned projection.", nameof(message));
        if (message.Identity.EffectKind is not (
            EventProjectorEffectKind.ProcessingPublication
            or EventProjectorEffectKind.CompletedPublication
            or EventProjectorEffectKind.FailedPublication))
            throw new ArgumentOutOfRangeException(nameof(message), "Only publication effects can be staged in the outbox.");
        ArgumentException.ThrowIfNullOrWhiteSpace(message.EventTypeName);
        ArgumentNullException.ThrowIfNull(message.EventPayload);
        if (message.EventPayload.Length == 0)
            throw new ArgumentException("The outbox payload cannot be empty.", nameof(message));
    }

}
