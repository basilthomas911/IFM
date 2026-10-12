using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage.EventSourceDb.CommandDeduplication;
using TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;
using TomasAI.IFM.Application.Storage.EventSourceDb.Persistence;
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
public sealed class EventSourceActorDbContext : ObjectDataRepository<EventSourceActorDbContext>,
      IEventSourceActorDbContext,
      ICommandAuditLogger,
      IDisposable,
      IAsyncDisposable
{
    internal readonly IBlackboardService _blackboardService;
    internal readonly IDbContextFactory _dbFactory;
    internal readonly ConcurrentDictionary<string, EventNameIdReadModel> _eventNameIdCache = new(StringComparer.Ordinal);
    internal readonly ConcurrentDictionary<Guid, Lazy<Task<bool>>> _legacyCommandReservations = new();
    internal readonly ConcurrentDictionary<string, AtomicPreparationLane> _atomicPreparationLanes = new(StringComparer.Ordinal);
    readonly ILogger _diagnosticLogger;
    internal readonly Lazy<CommandDuplicateCoordinator> _commandDuplicates = new(
        static () => new CommandDuplicateCoordinator(
            CommandDuplicateCoordinator.ReadConfiguredCapacity()),
        LazyThreadSafetyMode.ExecutionAndPublication);
    internal readonly CommandAuditPersistenceOptions _commandAuditOptions;
    internal readonly CommandAuditMessagePackCodec _commandAuditCodec = new();
    internal readonly Lazy<ICommandAuditWriter> _commandAuditWriter;
    readonly Lazy<IEventLogAppender> _eventLogAppender;
    internal readonly Lazy<IEventLogAppender> _atomicCommandEventAppender;
    internal readonly EventLogSqlLayout _eventLogSqlLayout;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "The database provider owns this injected logger; retain its category and constructor contract while the context supplies structured component names.")]
    public EventSourceActorDbContext(
        IDbConnectionSettings connectionSettings,
        IDbContextFactory dbFactory,
        IBlackboardService blackboardService,
        ILogger<DbProvider> logger,
        EventLogPersistenceOptions? eventLogPersistenceOptions = null,
        CommandAuditPersistenceOptions? commandAuditPersistenceOptions = null)
        : base((connectionSettings ?? throw new ArgumentNullException(nameof(connectionSettings)))[EventSourceActorDbConnection], logger)
    {
        _diagnosticLogger = logger;
        _blackboardService = IsArgumentNull.Set(blackboardService);
        _dbFactory = IsArgumentNull.Set(dbFactory);
        var eventOptions = (eventLogPersistenceOptions ?? new EventLogPersistenceOptions()).Validate();
        _eventLogSqlLayout = EventLogSqlLayout.ForProduction(eventOptions);
        _commandAuditOptions = (commandAuditPersistenceOptions ?? new CommandAuditPersistenceOptions()).Validate();
        var connectionString = connectionSettings[EventSourceActorDbConnection].ConnectionString;
        _commandAuditWriter = new(
            () => new PostgresCommandAuditWriter(connectionString, _commandAuditOptions),
            LazyThreadSafetyMode.ExecutionAndPublication);
        _eventLogAppender = new(
            () => this.CreateEventLogAppender(connectionString, eventOptions),
            LazyThreadSafetyMode.ExecutionAndPublication);
        _atomicCommandEventAppender = new(
            () => new BinaryCopyEventLogAppender(connectionString, eventOptions.UseLz4Compression, eventOptions),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    // Internal qualification seam only; public/DI construction keeps the production writers unchanged.
    // Both baseline and candidate require the same isolated database, even when batching is disabled.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "The database provider owns this injected logger; retain its category and constructor contract while the context supplies structured component names.")]
    internal EventSourceActorDbContext(
        IDbConnectionSettings connectionSettings, IDbContextFactory dbFactory,
        IBlackboardService blackboardService, ILogger<DbProvider> logger,
        EventLogPersistenceOptions options, CommandAuditPersistenceOptions? auditOptions,
        bool benchmarkBatchProjectionMarkers)
        : this(connectionSettings, dbFactory, blackboardService, logger, options, auditOptions)
    {
        var connection = connectionSettings[EventSourceActorDbConnection].ConnectionString;
        var layout = EventLogSqlLayout.ForBenchmark(connection, benchmarkBatchProjectionMarkers);
        options.Validate();
        if (options.WriteMode != EventLogWriteMode.BinaryCopy)
            throw new ArgumentException("Actor qualification requires the binary-copy writer.", nameof(options));
        _eventLogAppender = new(() => new BinaryCopyEventLogAppender(
            connection, options.UseLz4Compression, options, layout));
        _atomicCommandEventAppender = new(() => new BinaryCopyEventLogAppender(
            connection, options.UseLz4Compression, options, layout));
    }


    /// <summary>
    /// Gets the database context.
    /// </summary>
    public override EventSourceActorDbContext Database => this;

    /// <summary>Gets the actor event-source database read capability.</summary>
    public IEventSourceActorDbReadContext DbReader => this;

    /// <summary>Gets the actor event-source database write capability.</summary>
    public IEventSourceActorDbWriteContext DbWriter => this;

    /// <summary>
    /// Configuration key for the event-source database connection.
    /// </summary>
    public const string EventSourceActorDbConnection = "EventSourceActorDbConnection";

    /// <summary>
    /// Error message used when loading events fails.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1707", Justification = "Preserve the established public storage/serialized schema contract and identifiers consumed by callers.")]
    public const string ERR_EventDbContext_LoadEventsAsync = "EventSourceActorDbContext: Unable to execute LoadEventsAsync";

    /// <summary>
    /// Error message used when saving events fails.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1707", Justification = "Preserve the established public storage/serialized schema contract and identifiers consumed by callers.")]
    public const string ERR_EventDbContext_SaveEventsAsync = "EventSourceActorDbContext: Unable to execute SaveEventsAsync";

    /// <summary>
    /// Maps ordinal data from an <see cref="IObjectDataRecord"/> to a <see cref="CommandLogReadModel"/> instance.
    /// </summary>
    /// <param name="o">The object map reader containing the source data for the mapping operation.</param>
    /// <returns>A <see cref="CommandLogReadModel"/> populated with values retrieved from the specified object map reader.</returns>
    internal static CommandLogReadModel MapToCommandLog(IObjectDataRecord o)
        => new(
            CommandId: o.GetGuid(0),
            StreamId: o.GetString(1),
            AggregateName: o.GetEnum<BoundedContextName>(2),
            CommandName: o.GetString(3),
            CommandTimestamp: o.GetDateTime(4),
            CommandData: o.GetString(5),
            CommandPayload: o.IsNull(6) ? null : o.GetBytes(6),
            CommandPayloadFormat: o.IsNull(7) ? null : o.GetShort(7),
            CommandPayloadVersion: o.IsNull(8) ? null : o.GetInt(8),
            CommandPayloadSha256: o.IsNull(9) ? null : o.GetBytes(9)
        );

    /// <summary>
    /// Maps the provided object reader to an <see cref="EventStreamIdReadModel"/> instance.
    /// </summary>
    /// <param name="o">The object reader used to retrieve values for the <see cref="EventStreamIdReadModel"/> properties.</param>
    /// <returns>An <see cref="EventStreamIdReadModel"/> instance populated with values from the object reader.</returns>
    internal static EventStreamIdReadModel MapToEventStreamId(IObjectDataRecord o)
        => new(
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
        => new(
                EventStreamId: o.GetLong(0),
                EventName: o.GetString(1),
                EventTypeName: o.GetString(2),
                EventVersion: o.GetLong(3),
                EventData: o.GetBytes(4),
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

    internal static EventProjectorExecutionStateReadModel MapToEventProjectorExecutionState(
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
                EventData: o.GetBytes(4),
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
            EventData = o.GetBytes(4),
            StreamVersion = o.GetLong(7)
        };

    internal static long MapToLong<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => e.GetLong(0);
    internal static int MapToInt<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
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
            var eventStreamId = await this.InsertEventStreamAsync(eventStream, cancellationToken).ConfigureAwait(false);
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
        this.ValidateRequiredArgument(domainEvents, nameof(domainEvents));
        var streamId = await GetEventStreamIdAsync(eventStream, cancellationToken).ConfigureAwait(false);
        var entries = new List<EventLogAppendEntry>(domainEvents.Count);
        foreach (var e in domainEvents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var eventNameId = await GetEventNameIdFromDomainEventAsync(e, cancellationToken).ConfigureAwait(false);
            entries.Add(new EventLogAppendEntry(eventNameId, e));
        }
        try
        {
            var result = await _eventLogAppender.Value.AppendAsync(new EventLogAppendRequest(
                eventStream, streamId, commandId, entries, null, DateTime.UtcNow), cancellationToken).ConfigureAwait(false);
            return this.ApplyAssignments(domainEvents, result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new StorageException(ERR_EventDbContext_SaveEventsAsync, ex);
        }
    }

    /// <summary>Saves one atomic event batch using optimistic stream-version concurrency.</summary>
    public async Task<DomainEventCollection> SaveEventsAsync(
        string eventStream,
        Guid commandId,
        DomainEventCollection domainEvents,
        long expectedStreamVersion,
        CancellationToken cancellationToken)
    {
        this.ValidateRequiredArgument(domainEvents, nameof(domainEvents));
        this.ValidateNonnegativeArgument(expectedStreamVersion, nameof(expectedStreamVersion));

        var streamId = await GetEventStreamIdAsync(eventStream, cancellationToken).ConfigureAwait(false);
        var entries = new List<EventLogAppendEntry>(domainEvents.Count);
        foreach (var domainEvent in domainEvents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            entries.Add(new EventLogAppendEntry(
                await GetEventNameIdFromDomainEventAsync(domainEvent, cancellationToken).ConfigureAwait(false),
                domainEvent));
        }
        try
        {
            var result = await _eventLogAppender.Value.AppendAsync(new EventLogAppendRequest(
                eventStream, streamId, commandId, entries, expectedStreamVersion, DateTime.UtcNow), cancellationToken).ConfigureAwait(false);
            return this.ApplyAssignments(domainEvents, result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        finally { /* Repository disposal belongs to the owning context. */ }
    }

    public Task<DomainEventCollection> SaveCommandEventsAtomicallyAsync(
        ICommand command,
        DomainEventCollection domainEvents,
        long expectedStreamVersion,
        CancellationToken cancellationToken = default)
    {
        this.ValidateRequiredArgument(command, nameof(command));
        this.ValidateRequiredArgument(domainEvents, nameof(domainEvents));
        this.ValidateNonnegativeArgument(expectedStreamVersion, nameof(expectedStreamVersion));
        return this.SaveCommandEventsDeduplicatedAsync(
            command, domainEvents, expectedStreamVersion, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_commandAuditWriter.IsValueCreated)
            await _commandAuditWriter.Value.DisposeAsync().ConfigureAwait(false);
        if (_eventLogAppender.IsValueCreated)
            await _eventLogAppender.Value.DisposeAsync().ConfigureAwait(false);
        if (_atomicCommandEventAppender.IsValueCreated)
            await _atomicCommandEventAppender.Value.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Drains owned persistence writers for hosts whose dependency-injection container exposes only synchronous
    /// shutdown. Normal command processing never uses this blocking bridge.
    /// </summary>
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    public Task InsertCommandLogAsync(ICommand command, DateTime commandTimestamp, string commandData)
        => InsertCommandLogAsync(command, commandTimestamp, commandData, CancellationToken.None);

    public Task InsertCommandLogAsync(
        ICommand command,
        DateTime commandTimestamp,
        string commandData,
        CancellationToken cancellationToken)
    {
        this.ValidateRequiredArgument(command, nameof(command));
        this.ValidateTextArgument(commandData, nameof(commandData));
        cancellationToken.ThrowIfCancellationRequested();

        var candidate = new Lazy<Task<bool>>(
            () => TryInsertCommandLogAsync(
                command,
                commandTimestamp,
                commandData,
                cancellationToken),
            LazyThreadSafetyMode.ExecutionAndPublication);
        var reservation = _legacyCommandReservations.GetOrAdd(command.CommandId, candidate);
        return this.ObserveLegacyReservationAsync(command.CommandId, reservation);
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
        this.ValidateRequiredArgument(command, nameof(command));
        this.ValidateTextArgument(commandData, nameof(commandData));

        return await _commandDuplicates.Value.TryAcceptAsync(
                command.CommandId,
                token => this.InsertCommandLogCoreAsync(command, commandTimestamp, commandData, token),
                cancellationToken)
            .ConfigureAwait(false);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2139", Justification = "Log persistence failure with its request identity before preserving the original exception for the caller transaction boundary.")]
    public async ValueTask<CommandAuditReservation> TryReserveAsync(ICommand command, CancellationToken cancellationToken = default)
    {
        this.ValidateRequiredArgument(command, nameof(command));
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            var result = await this.TryReserveCoreAsync(command, cancellationToken).ConfigureAwait(false);
            var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            if (elapsed >= 250 && _diagnosticLogger.IsEnabled(LogLevel.Information))
            {
                object subject = command.Subject;
                StorageDiagnosticLog.SlowReservation(_diagnosticLogger,
                    nameof(EventSourceActorDbContext), nameof(TryReserveAsync), command.CommandId, subject, elapsed);
            }
            return result;
        }
        catch (CommandAuditPayloadConflictException exception)
        {
            StorageDiagnosticLog.ReservationConflict(_diagnosticLogger, exception,
                nameof(EventSourceActorDbContext), nameof(TryReserveAsync), command.CommandId, command.Subject, _commandAuditOptions.WriteMode);
            throw;
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task InsertEventProjectorStateAsync(EventProjectorStateReadModel state)
        => await InsertEventProjectorStateAsync(state, CancellationToken.None).ConfigureAwait(false);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3236", Justification = "The validated member belongs to this public input argument; explicitly report its root parameter rather than exposing a member expression as the parameter name.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task InsertEventProjectorStateAsync(
        EventProjectorStateReadModel state,
        CancellationToken cancellationToken)
    {
        this.ValidateRequiredArgument(state, nameof(state));
        this.ValidateTextArgument(state.ProjectorName, nameof(state));
        var now = DateTime.UtcNow;
        var createdTimestamp = state.CreatedTimestamp == default ? now : state.CreatedTimestamp;
        await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.UpsertEventProjectorState)}", this.EventLogSql(EventSourceDbSql.UpsertEventProjectorState))
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
        this.ValidateTextArgument(projectorName, nameof(projectorName));
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
        this.ValidatePositiveArgument(eventId, nameof(eventId));
        return await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventLogByEventVersion)}", this.EventLogSql(EventSourceDbSql.GetEventLogByEventVersion))
            .SetParameters(new GetEventLogByEventVersion(eventId))
            .ExecuteSingleAsync<EventLogReadModel>(MapToEventLog, cancellationToken)
            .ConfigureAwait(false);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3236", Justification = "The validated member belongs to this public input argument; explicitly report its root parameter rather than exposing a member expression as the parameter name.")]
    public async Task<EventProjectorExecutionStateReadModel?> TryCreateEventProjectorExecutionStateAsync(
        EventProjectorExecutionStateReadModel state,
        CancellationToken cancellationToken = default)
    {
        this.ValidateRequiredArgument(state, nameof(state));
        this.ValidateProjectorIdentity(state.EventId, state.ProjectorName);
        this.ValidateTextArgument(state.ActorName, nameof(state));
        var createdTimestamp = this.RequireUtc(
            state.CreatedTimestamp == default ? DateTime.UtcNow : state.CreatedTimestamp,
            nameof(state.CreatedTimestamp));
        var updatedAtUtc = this.RequireUtc(
            state.UpdatedAtUtc == default ? createdTimestamp : state.UpdatedAtUtc,
            nameof(state.UpdatedAtUtc));

        return await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.TryCreateEventProjectorExecutionState)}", this.EventLogSql(EventSourceDbSql.TryCreateEventProjectorExecutionState))
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
        this.ValidateProjectorIdentity(eventId, projectorName);
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
        this.ValidateTextArgument(projectorName, nameof(projectorName));
        this.ValidatePositiveArgument(eventStreamId, nameof(eventStreamId));
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
        this.ValidateProjectorIdentity(eventId, projectorName);
        this.ValidateExecutionToken(executionToken);
        nowUtc = this.RequireUtc(nowUtc, nameof(nowUtc));
        var leaseExpiresAtUtc = this.GetLeaseExpiry(nowUtc, leaseDuration);
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
        this.ValidateProjectorIdentity(eventId, projectorName);
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
        this.ValidateProjectorIdentity(eventId, projectorName);
        this.ValidateExecutionToken(executionToken);
        this.ValidateNonnegativeArgument(expectedRevision, nameof(expectedRevision));
        nowUtc = this.RequireUtc(nowUtc, nameof(nowUtc));
        var leaseExpiresAtUtc = this.GetLeaseExpiry(nowUtc, leaseDuration);
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
        this.ValidateTransition(transition, terminal: false);
        if (transition.Outcome != EventProjectorOutcomeType.Retrying)
            throw new ArgumentOutOfRangeException(nameof(transition), "A released execution must be retryable.");
        if (!transition.NextAttemptAtUtc.HasValue || !transition.LastErrorAtUtc.HasValue)
            throw new ArgumentException("A released execution requires retry and error timestamps.", nameof(transition));
        nowUtc = this.RequireUtc(nowUtc, nameof(nowUtc));
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
        this.ValidateTransition(transition, terminal: false);
        nowUtc = this.RequireUtc(nowUtc, nameof(nowUtc));
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
                this.ValidateOptionalUtc(transition.NextAttemptAtUtc, nameof(transition.NextAttemptAtUtc)),
                this.ValidateOptionalUtc(transition.LastErrorAtUtc, nameof(transition.LastErrorAtUtc)),
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
        this.ValidateTransition(transition, terminal: true);
        nowUtc = this.RequireUtc(nowUtc, nameof(nowUtc));
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
                this.ValidateOptionalUtc(transition.LastErrorAtUtc, nameof(transition.LastErrorAtUtc)),
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
        this.ValidateTransition(transition, terminal: false);
        this.ValidateOutboxMessage(transition, message);
        nowUtc = this.RequireUtc(nowUtc, nameof(nowUtc));
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
            this.ValidateOptionalUtc(transition.NextAttemptAtUtc, nameof(transition.NextAttemptAtUtc)),
            this.ValidateOptionalUtc(transition.LastErrorAtUtc, nameof(transition.LastErrorAtUtc)),
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
        this.ValidateTransition(transition, terminal: true);
        this.ValidateOutboxMessage(transition, message);
        nowUtc = this.RequireUtc(nowUtc, nameof(nowUtc));
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
            this.ValidateOptionalUtc(transition.LastErrorAtUtc, nameof(transition.LastErrorAtUtc)),
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
        this.ValidateTextArgument(projectorName, nameof(projectorName));
        this.ValidateExecutionToken(dispatchToken);
        if (batchSize is < 1 or > 2_048)
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        nowUtc = this.RequireUtc(nowUtc, nameof(nowUtc));
        var leaseExpiresAtUtc = this.GetLeaseExpiry(nowUtc, leaseDuration);
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
        this.ValidateRequiredArgument(message, nameof(message));
        nowUtc = this.RequireUtc(nowUtc, nameof(nowUtc));
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
        this.ValidateRequiredArgument(message, nameof(message));
        if (status is not EventProjectorOutboxStatus.Retrying and not EventProjectorOutboxStatus.Failed)
            throw new ArgumentOutOfRangeException(nameof(status));
        nowUtc = this.RequireUtc(nowUtc, nameof(nowUtc));
        nextAttemptAtUtc = this.ValidateOptionalUtc(nextAttemptAtUtc, nameof(nextAttemptAtUtc));
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
        this.ValidateTextArgument(projectorName, nameof(projectorName));
        if (!Enum.IsDefined(status))
            throw new ArgumentOutOfRangeException(nameof(status));
        this.ValidateNonnegativeArgument(afterEventId, nameof(afterEventId));
        if (batchSize is < 1 or > 2_048)
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        return [.. await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventProjectorOperationalStatePage)}", this.EventLogSql(EventSourceDbSql.GetEventProjectorOperationalStatePage))
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
        this.ValidateTextArgument(projectorName, nameof(projectorName));
        nowUtc = this.RequireUtc(nowUtc, nameof(nowUtc));
        return await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventProjectorOperationalSnapshot)}", this.EventLogSql(EventSourceDbSql.GetEventProjectorOperationalSnapshot))
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
        this.ValidateProjectorIdentity(eventId, projectorName);
        nowUtc = this.RequireUtc(nowUtc, nameof(nowUtc));
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
        this.ValidateProjectorIdentity(eventId, projectorName);
        this.ValidateTextArgument(reason, nameof(reason));
        if (reason.Length > 2_000)
            throw new ArgumentOutOfRangeException(nameof(reason));
        nowUtc = this.RequireUtc(nowUtc, nameof(nowUtc));
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
        this.ValidateTextArgument(projectorName, nameof(projectorName));
        this.ValidateRequiredArgument(eventNames, nameof(eventNames));
        if (eventNames.Count == 0)
            return [];
        this.ValidateNonnegativeArgument(afterEventId, nameof(afterEventId));
        if (batchSize is < 1 or > 2_048)
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        nowUtc = this.RequireUtc(nowUtc, nameof(nowUtc));

        return [.. await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventProjectorRecoveryPage)}", this.EventLogSql(EventSourceDbSql.GetEventProjectorRecoveryPage))
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
        this.ValidateTextArgument(projectorName, nameof(projectorName));
        this.ValidateRequiredArgument(eventNames, nameof(eventNames));
        if (eventNames.Count == 0)
            return [];

        return await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetUncompletedEventProjectorEvents)}", this.EventLogSql(EventSourceDbSql.GetUncompletedEventProjectorEvents))
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
        => await (_dbFactory.ActorEventSourceDb
                .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.UpdateCommandLog)}", EventSourceDbSql.UpdateCommandLog)
                .SetParameters(new UpdateCommandLog(
                    commandId: commandId,
                    commandStatus: $"{commandStatus}",
                    updateTimestamp: updateTimestamp
                ))
                .ExecuteCommandAsync(cancellationToken)).ConfigureAwait(false);

    /// <summary>
    /// Deletes an event log entry from the database based on the specified event version.
    /// </summary>
    /// <remarks>This method performs an asynchronous database operation to delete the event log entry. Ensure
    /// that the specified <paramref name="eventVersion"/> corresponds to an existing event log.</remarks>
    /// <param name="eventVersion">The version of the event log to delete. Must be a positive integer.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task DeleteEventLogAsync(long eventVersion)
        => await (_dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.DeleteEventLog)}", this.EventLogSql(EventSourceDbSql.DeleteEventLog))
            .SetParameters(new DeleteEventLog(eventVersion))
            .ExecuteCommandAsync()).ConfigureAwait(false);

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
        this.ValidateRequiredArgument(eventVersions, nameof(eventVersions));
        foreach (var eventVersion in eventVersions)
            await (DeleteEventLogAsync(eventVersion)).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously deletes all event log entries associated with the specified stream identifier.
    /// </summary>
    /// <param name="streamId">The unique identifier of the event stream whose log entries are to be deleted.</param>
    /// <returns>A task that represents the asynchronous delete operation.</returns>
    public async Task DeleteEventLogByStreamIdAsync(long streamId)
        => await (_dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.DeleteEventLogByStreamId)}", this.EventLogSql(EventSourceDbSql.DeleteEventLogByStreamId))
            .SetParameters(new DeleteEventLogByStreamId(streamId))
            .ExecuteCommandAsync()).ConfigureAwait(false);

    /// <summary>
    /// Asynchronously deletes the event stream with the specified identifier from the data store.
    /// </summary>
    /// <param name="eventStreamId">The unique identifier of the event stream to delete.</param>
    /// <returns>A task that represents the asynchronous delete operation.</returns>
    public async Task DeleteEventStreamByIdAsync(long eventStreamId)
        => await (_dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.DeleteEventStreamById)}", EventSourceDbSql.DeleteEventStreamById)
            .SetParameters(new DeleteEventStreamById(eventStreamId))
            .ExecuteCommandAsync()).ConfigureAwait(false);

    /// <summary>
    /// Retrieves the command log associated with the specified command ID.
    /// </summary>
    /// <remarks>This method asynchronously fetches the command log from the database using the provided
    /// command ID. Ensure that the <paramref name="commandId"/> is valid and corresponds to an existing
    /// command.</remarks>
    /// <param name="commandId">The unique identifier of the command whose log is to be retrieved.</param>
    /// <returns>A <see cref="CommandLogReadModel"/> representing the command log if found; otherwise, <see langword="null"/>.</returns>
    public async Task<CommandLogReadModel?> GetCommandLogAsync(Guid commandId)
        => await (_dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetCommandLog)}", EventSourceDbSql.GetCommandLog)
            .SetParameters(new GetCommandLog(commandId))
            .ExecuteSingleAsync<CommandLogReadModel>(MapToCommandLog)).ConfigureAwait(false);

    public async Task<bool> HasEventForCommandAsync(Guid commandId)
        => await (_dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.HasEventForCommand)}", this.EventLogSql(EventSourceDbSql.HasEventForCommand))
            .SetParameters(new GetCommandLog(commandId))
            .ExecuteScalarAsync(static value => value.GetBool(0))).ConfigureAwait(false);

    /// <summary>
    /// Retrieves the event stream ID from the database based on the specified event stream name.
    /// </summary>
    /// <remarks>This method queries the database to retrieve the ID associated with the given event stream
    /// name. Ensure that the provided <paramref name="eventStream"/> corresponds to a valid entry in the
    /// database.</remarks>
    /// <param name="eventStream">The name of the event stream to query. Cannot be null or empty.</param>
    /// <returns>An <see cref="EventStreamIdReadModel"/> containing the event stream ID if found; otherwise, <see
    /// langword="null"/>.</returns>
    /// <summary>Reads the last committed source version; filtered state windows do not define stream concurrency.</summary>
    public async Task<long> GetCommittedStreamVersionAsync(long eventStreamId, CancellationToken cancellationToken = default)
    {
        this.ValidatePositiveArgument(eventStreamId, nameof(eventStreamId));
        return await Database.Use("EventSource.StreamVersion",
            this.EventLogSql("SELECT COALESCE(MAX(StreamVersion),0) FROM event_log WHERE EventStreamId=$1;"))
            .SetParameters(new GetEventLogByEventStreamId(eventStreamId))
            .ExecuteSingleAsync(static row => row.GetLong(0), cancellationToken).ConfigureAwait(false);
    }

    public async Task<EventStreamIdReadModel?> GetEventStreamIdFromDbAsync(string eventStream)
        => await (_dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventStreamId)}", EventSourceDbSql.GetEventStreamId)
            .SetParameters(new GetEventStreamId(eventStream))
            .ExecuteSingleAsync<EventStreamIdReadModel>(MapToEventStreamId)).ConfigureAwait(false);

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
        => await this.GetEventNameIdFromTypeAsync(domainEvent.GetType(), cancellationToken).ConfigureAwait(false);

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
        var snapshotEventNameId = await (this.GetEventNameIdFromTypeAsync<TSnapshot>()).ConfigureAwait(false);
        var db = _dbFactory.ActorEventSourceDb;
        var maxEventVersion = await (db.Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetMaxEventVersion)}", this.EventLogSql(EventSourceDbSql.GetMaxEventVersion))
            .SetParameters(new GetMaxEventVersion(eventStreamId, snapshotEventNameId))
            .ExecuteScalarAsync(MapToLong)).ConfigureAwait(false);
        return maxEventVersion > 0
            ? await (db.Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventLogByMaxEventVersion)}", this.EventLogSql(EventSourceDbSql.GetEventLogByMaxEventVersion))
                .SetParameters(new GetEventLogByMaxEventVersion(eventStreamId, maxEventVersion))
                .ExecuteQueryAsync<EventStreamReadModel>(MapToEventStream)).ConfigureAwait(false)
            : await (db.Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventLogByEventStreamId)}", this.EventLogSql(EventSourceDbSql.GetEventLogByEventStreamId))
                .SetParameters(new GetEventLogByEventStreamId(eventStreamId))
                .ExecuteQueryAsync<EventStreamReadModel>(MapToEventStream)).ConfigureAwait(false);
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
        await (_dbFactory.ActorEventSourceDb
                .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventLogByEventStreamId)}", this.EventLogSql(EventSourceDbSql.GetEventLogByEventStreamId))
                .SetParameters(new GetEventLogByEventStreamId(eventStreamId))
                .ExecuteMapReduceAsync(EventStreamMapper, reducerAction, cancellationToken)).ConfigureAwait(false);

        EventStreamReadModel EventStreamMapper(IObjectDataRecord o)
        {
            eventStream.EventVersion = o.GetLong(3);
            eventStream.EventTypeName = o.GetString(2);
            eventStream.EventData = o.GetBytes(4);
            eventStream.StreamVersion = o.GetLong(7);
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
        var eventNameId = await this.GetEventNameIdFromTypeAsync<TEvent>(cancellationToken).ConfigureAwait(false);
        await (_dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventLogLastNRangeByEventName)}", this.EventLogSql(EventSourceDbSql.GetEventLogLastNRangeByEventName))
            .SetParameters(new GetEventLogLastNRangeByEventName(
                eventStreamId,
                eventNameId,
                Math.Max(0, lastNRange)))
            .ExecuteMapReduceAsync(MapToEventStream, reducerAction, cancellationToken)).ConfigureAwait(false);
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
        var snapshotEventNameId = await this.GetEventNameIdFromTypeAsync<TSnapshot>(cancellationToken).ConfigureAwait(false);
        var db = _dbFactory.ActorEventSourceDb;
        var maxEventVersion = await (db.Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetMaxEventVersion)}", this.EventLogSql(EventSourceDbSql.GetMaxEventVersion))
            .SetParameters(new GetMaxEventVersion(eventStreamId, snapshotEventNameId))
            .ExecuteScalarAsync(MapToLong, cancellationToken)).ConfigureAwait(false);
        if (maxEventVersion > 0)
            await (db.Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventLogByMaxEventVersion)}", this.EventLogSql(EventSourceDbSql.GetEventLogByMaxEventVersion))
                .SetParameters(new GetEventLogByMaxEventVersion(eventStreamId, maxEventVersion))
                .ExecuteMapReduceAsync(EventStreamMapper, reducerAction, cancellationToken)).ConfigureAwait(false);
        else
            await (db.Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventLogByEventStreamId)}", this.EventLogSql(EventSourceDbSql.GetEventLogByEventStreamId))
                .SetParameters(new GetEventLogByEventStreamId(eventStreamId))
                .ExecuteMapReduceAsync(EventStreamMapper, reducerAction, cancellationToken)).ConfigureAwait(false);

        EventStreamReadModel EventStreamMapper(IObjectDataRecord o)
        {
            eventStream.EventVersion = o.GetLong(3);
            eventStream.EventTypeName = o.GetString(2);
            eventStream.EventData = o.GetBytes(4);
            eventStream.StreamVersion = o.GetLong(7);
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
        var snapshotEventNameId = await this.GetEventNameIdFromTypeAsync<TSnapshot>(cancellationToken).ConfigureAwait(false);
        var rangeEventNameId = await this.GetEventNameIdFromTypeAsync<TRangeEvent>(cancellationToken).ConfigureAwait(false);
        await (_dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventLogFromSnapshotLastNRange)}", this.EventLogSql(EventSourceDbSql.GetEventLogFromSnapshotLastNRange))
            .SetParameters(new GetEventLogFromSnapshotLastNRange(
                eventStreamId,
                snapshotEventNameId,
                rangeEventNameId,
                Math.Max(0, lastNRange)))
            .ExecuteMapReduceAsync(MapToEventStream, reducerAction, cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>
    /// Loads the full event stream into memory for the specified actor state type.
    /// </summary>
    /// <typeparam name="TState">The actor state type that implements <see cref="IActorState{TState}"/>.</typeparam>
    /// <param name="eventStreamId">The unique identifier of the event stream to load.</param>
    /// <returns>An <see cref="ICollection{T}"/> of <see cref="EventStreamReadModel"/> entries for the stream.</returns>
    public async ValueTask<ICollection<EventStreamReadModel>> LoadActorEventStreamAsync<TState>(long eventStreamId)
        where TState : IActorState<TState>
            => await (this.GetEventStreamAsync(eventStreamId)).ConfigureAwait(false);

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
        var eventNameId = await (this.GetEventNameIdFromTypeAsync<TEvent>()).ConfigureAwait(false);
        return await (_dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventLogLastNRangeByEventName)}", this.EventLogSql(EventSourceDbSql.GetEventLogLastNRangeByEventName))
            .SetParameters(new GetEventLogLastNRangeByEventName(
                eventStreamId,
                eventNameId,
                Math.Max(0, lastNRange)))
            .ExecuteQueryAsync<EventStreamReadModel>(MapToEventStream)).ConfigureAwait(false);
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
           => await (GetEventsFromSnapshotAsync<TSnapshot>(eventStreamId)).ConfigureAwait(false);


}
