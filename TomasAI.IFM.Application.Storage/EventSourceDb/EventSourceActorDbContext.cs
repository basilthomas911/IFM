using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;
using TomasAI.IFM.Application.Storage.EventSourceDb.EventLog;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventProjector.ReadModels;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.EventSourceDb;

/// <summary>
/// Database context for actor event-sourcing operations (load, save, delete, map-reduce) against the event store.
/// </summary>
/// <remarks>
/// <see cref="EventSourceActorDbContext"/> interacts with an event-sourced database to retrieve event streams, save
/// domain events, and manage event logs. Typed process-local caches accelerate stable identities;
/// PostgreSQL and <see cref="IDbContextFactory"/> supply authoritative database access.
/// Utility mapping methods convert data records to strongly-typed view models, and transactional save operations
/// ensure consistency.
/// </remarks>
public sealed class EventSourceActorDbContext : ObjectDataRepository<EventSourceActorDbContext>,
      IEventSourceActorDbContext
{
    internal readonly TomasAI.IFM.Framework.Caching.BoundedAsyncCache<string, long> _streamIdentities = new();
    internal readonly Func<string, CancellationToken, Task<long>> _streamIdentityLoader;
    internal readonly IDbContextFactory _dbFactory;
    internal readonly TimeProvider _businessClock;
    /// <summary>Gets the business clock used to timestamp persisted messages.</summary>
    public TimeProvider BusinessClock => _businessClock;
    internal readonly ConcurrentDictionary<string, EventNameIdReadModel> _eventNameIdCache = new(StringComparer.Ordinal);
    internal readonly ConcurrentDictionary<string, AtomicPreparationLane> _atomicPreparationLanes = new(StringComparer.Ordinal);
    internal readonly CommandAuditPersistenceOptions _commandAuditOptions;
    internal readonly CommandAuditMessagePackCodec _commandAuditCodec = new();
    internal readonly Lazy<ICommandAuditWriter> _commandAuditWriter;
    readonly Lazy<IEventLogAppender> _eventLogAppender;
    readonly Lazy<IPostgresEventTransaction> _aggregateCheckpointTransactions;
    internal readonly Lazy<IEventLogAppender> _atomicCommandEventAppender;
    internal readonly EventLogSqlLayout _eventLogSqlLayout;

    /// <summary>Initializes the actor event-source context and its configured persistence writers.</summary>
    /// <param name="connectionSettings">Application database connection settings.</param>
    /// <param name="dbFactory">Factory providing access to event-source repository contexts.</param>

    /// <param name="logger">Logger for database provider diagnostics.</param>
    /// <param name="eventLogPersistenceOptions">The event log persistence configuration.</param>
    /// <param name="commandAuditPersistenceOptions">The command audit persistence configuration.</param>
    /// <param name="timeProvider">The business clock used for event and command timestamps.</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "The database provider owns this injected logger; retain its category and constructor contract while the context supplies structured component names.")]
    public EventSourceActorDbContext(
        IDbConnectionSettings connectionSettings,
        IDbContextFactory dbFactory,
        ILogger<DbProvider> logger,
        EventLogPersistenceOptions? eventLogPersistenceOptions = null,
        CommandAuditPersistenceOptions? commandAuditPersistenceOptions = null, TimeProvider? timeProvider = null)
        : base(connectionSettings[EventSourceActorDbConnection], logger)
    {
        _businessClock = timeProvider ?? TimeProvider.System;
        _streamIdentityLoader = async (stream, token) =>
        {
            var id = await this.InsertEventStreamAsync(stream, token)
            .ConfigureAwait(false);
            if (id <= 0) throw new InvalidOperationException("PostgreSQL returned an invalid stream identity.");
            return id;
        };
        _dbFactory = dbFactory;
        var eventOptions = (eventLogPersistenceOptions ?? new EventLogPersistenceOptions()).Validate();
        _eventLogSqlLayout = EventLogSqlLayout.ForProduction(eventOptions);
        _aggregateCheckpointTransactions = new(() => new PostgresEventTransaction(connectionSettings, eventOptions, timeProvider: _businessClock));
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
        ILogger<DbProvider> logger,
        EventLogPersistenceOptions options, CommandAuditPersistenceOptions? auditOptions,
        bool benchmarkBatchProjectionMarkers, TimeProvider? timeProvider = null)
        : this(connectionSettings, dbFactory, logger, options, auditOptions, timeProvider)
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

    /// <summary>Attempts the durable command audit insertion using the supplied encoded facts.</summary>
    /// <param name="envelope">The command audit identity and provider payload.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if this request inserted the command audit; false if it already exists.</returns>
    public async ValueTask<bool> TryInsertCommandAuditAsync(CommandAuditEnvelope envelope, CancellationToken cancellationToken = default)
        => (await _commandAuditWriter.Value.ReserveAsync(envelope, cancellationToken)
            .ConfigureAwait(false)).Accepted;


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
            CommandPayloadSha256: o.IsNull(9) ? null : o.GetBytes(9),
            Origin: MapToOrigin(o)
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
    internal static MessageOrigin? MapToOrigin(IObjectDataRecord record)
        => record.TryGetString("MessageOriginJson", out var json) ? MessageOriginJson.Deserialize(json) : null;

    /// <summary>Maps the original event columns followed by the stream name.</summary>
    internal static EventStreamSourceReadModel MapToMaintenanceSource(System.Data.Common.DbDataReader row)
        => new(MapToEventLog(new AdoNetDataRecord().SetReader(row)), row.GetString(8));

    internal static EventLogReadModel MapToEventLog(IObjectDataRecord o)
        => new(
                EventStreamId: o.GetLong(0),
                EventName: o.GetString(1),
                EventTypeName: o.GetString(2),
                EventVersion: o.GetLong(3),
                EventData: o.GetBytes(4),
                CommandId: o.GetGuid(5),
                EventTimestamp: o.GetString(6),
                StreamVersion: o.GetLong(7),
                Origin: MapToOrigin(o)
            );

    /// <summary>
    /// Maps durable projection state from the event-source database.
    /// </summary>
    /// <param name="o">The o used by this operation.</param>
    /// <returns>The typed result of the event-source operation.</returns>
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

    /// <summary>Maps selected event-store columns to the typed result.</summary>
    /// <param name="o">The o used by this operation.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    internal static EventProjectorExecutionStateReadModel MapToEventProjectorExecutionState(
        IObjectDataRecord o)
        => MapToEventProjectorExecutionState(o, 0);

    /// <summary>Maps selected event-store columns to the typed result.</summary>
    /// <param name="o">The o used by this operation.</param>
    /// <param name="offset">The offset used by this operation.</param>
    /// <returns>The typed result of the event-source operation.</returns>
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

    /// <summary>Maps selected event-store columns to the typed result.</summary>
    /// <param name="o">The o used by this operation.</param>
    /// <returns>The typed result of the event-source operation.</returns>
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
                StreamVersion: o.GetLong(7),
                Origin: MapToOrigin(o)),
            State: MapToEventProjectorExecutionState(o, 8));

    /// <summary>Maps selected event-store columns to the typed result.</summary>
    /// <param name="o">The o used by this operation.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    internal static EventProjectorStreamCheckpointReadModel MapToEventProjectorStreamCheckpoint(
        IObjectDataRecord o)
        => new(
            ProjectorName: o.GetString(0),
            EventStreamId: o.GetLong(1),
            LastAppliedStreamVersion: o.GetLong(2),
            LastAppliedEventId: o.GetLong(3),
            Revision: o.GetLong(4),
            UpdatedAtUtc: o.GetDateTime(5));

    /// <summary>Maps selected event-store columns to the typed result.</summary>
    /// <param name="o">The o used by this operation.</param>
    /// <returns>The typed result of the event-source operation.</returns>
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
            DispatchLeaseExpiresAtUtc: o.GetDateTime(13),
            Origin: MapToOrigin(o));

    /// <summary>Maps selected event-store columns to the typed result.</summary>
    /// <param name="o">The o used by this operation.</param>
    /// <returns>The typed result of the event-source operation.</returns>
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
            StreamVersion = o.GetLong(7),
            Origin = MapToOrigin(o)
        };

    /// <summary>Maps selected event-store columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the event-source operation.</returns>
    internal static long MapToLong<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => e.GetLong(0);
    /// <summary>Maps selected event-store columns to the typed result.</summary>
    /// <param name="e">The e used by this operation.</param>
    /// <typeparam name="TDataRecord">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the event-source operation.</returns>
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
    public Task<long> GetEventStreamIdAsync(string eventStream)
        => GetEventStreamIdAsync(eventStream, CancellationToken.None);

    /// <summary>Get Event Stream Id using the event-source persistence services.</summary>
    /// <param name="eventStream">The event stream name.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    public Task<long> GetEventStreamIdAsync(string eventStream, CancellationToken cancellationToken)
        => this.ResolveEventStreamIdAsync(eventStream, cancellationToken);

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
        => await SaveEventsAsync(eventStream, commandId, domainEvents, CancellationToken.None)
            .ConfigureAwait(false);

    /// <summary>Appends ordered domain events and assigns their committed event and stream versions.</summary>
    /// <param name="eventStream">The event stream name.</param>
    /// <param name="commandId">The originating command identifier.</param>
    /// <param name="domainEvents">The ordered events to persist.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    public async Task<DomainEventCollection> SaveEventsAsync(
        string eventStream,
        Guid commandId,
        DomainEventCollection domainEvents,
        CancellationToken cancellationToken)
    {
        var streamId = await GetEventStreamIdAsync(eventStream, cancellationToken)
            .ConfigureAwait(false);
        var entries = new List<EventLogAppendEntry>(domainEvents.Count);
        foreach (var e in domainEvents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var eventNameId = await GetEventNameIdFromDomainEventAsync(e, cancellationToken)
            .ConfigureAwait(false);
            MessageOrigins.ForPublication(e, _businessClock);
            entries.Add(new EventLogAppendEntry(eventNameId, e));
        }
        try
        {
            var result = await _eventLogAppender.Value.AppendAsync(new EventLogAppendRequest(
                eventStream, streamId, commandId, entries, null, _businessClock.GetUtcNow().UtcDateTime), cancellationToken)
            .ConfigureAwait(false);
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
    /// <param name="eventStream">The event stream name.</param>
    /// <param name="commandId">The originating command identifier.</param>
    /// <param name="domainEvents">The ordered events to persist.</param>
    /// <param name="expectedStreamVersion">The committed stream version required for optimistic concurrency.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    public async Task<DomainEventCollection> SaveEventsAsync(
        string eventStream,
        Guid commandId,
        DomainEventCollection domainEvents,
        long expectedStreamVersion,
        CancellationToken cancellationToken)
    {

        var streamId = await GetEventStreamIdAsync(eventStream, cancellationToken)
            .ConfigureAwait(false);
        var entries = new List<EventLogAppendEntry>(domainEvents.Count);
        foreach (var domainEvent in domainEvents)
        {
            MessageOrigins.ForPublication(domainEvent, _businessClock);
            cancellationToken.ThrowIfCancellationRequested();
            entries.Add(new EventLogAppendEntry(
                await GetEventNameIdFromDomainEventAsync(domainEvent, cancellationToken)
            .ConfigureAwait(false),
                domainEvent));
        }
        try
        {
            var result = await _eventLogAppender.Value.AppendAsync(new EventLogAppendRequest(
                eventStream, streamId, commandId, entries, expectedStreamVersion, _businessClock.GetUtcNow().UtcDateTime), cancellationToken)
            .ConfigureAwait(false);
            return this.ApplyAssignments(domainEvents, result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        finally { /* Repository disposal belongs to the owning context. */ }
    }

    /// <summary>Persists the prepared audit envelope and supplied event batch in one PostgreSQL transaction.</summary>
    /// <param name="audit">The qualified command audit envelope.</param>
    /// <param name="domainEvents">The ordered events to persist.</param>
    /// <param name="expectedStreamVersion">The optimistic stream version.</param>
    /// <param name="cancellationToken">Cancels the persistence operation.</param>
    /// <returns>The events after durable identifiers are assigned.</returns>
    public Task<DomainEventCollection> PersistCommandEventBatchAsync(CommandAuditEnvelope audit,
        DomainEventCollection domainEvents, long expectedStreamVersion, CancellationToken cancellationToken = default)
        => this.PrepareAndSaveCommandEventsAtomicallyAsync(this.AcquireAtomicPreparation(audit.StreamId),
            domainEvents, expectedStreamVersion, audit, cancellationToken);

    /// <summary>Releases event-store persistence writers during host shutdown.</summary>
    /// <returns>The completion of the persistence operation.</returns>
    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        _streamIdentities.Dispose();
        if (_commandAuditWriter.IsValueCreated)
            await _commandAuditWriter.Value.DisposeAsync()
            .ConfigureAwait(false);
        if (_eventLogAppender.IsValueCreated)
            await _eventLogAppender.Value.DisposeAsync()
            .ConfigureAwait(false);
        if (_atomicCommandEventAppender.IsValueCreated)
            await _atomicCommandEventAppender.Value.DisposeAsync()
            .ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Drains owned persistence writers for hosts whose dependency-injection container exposes only synchronous
    /// shutdown. Normal command processing never uses this blocking bridge.
    /// </summary>
    void IDisposable.Dispose() => ((IAsyncDisposable)this).DisposeAsync().AsTask().GetAwaiter().GetResult();

    /// <summary>Insert Command Log using the event-source persistence services.</summary>
    /// <param name="command">The command being audited or committed.</param>
    /// <param name="commandTimestamp">The command audit timestamp.</param>
    /// <param name="commandData">The serialized command audit payload.</param>
    /// <returns>The completion of the persistence operation.</returns>
    public Task InsertCommandLogAsync(ICommand command, DateTime commandTimestamp, string commandData)
        => InsertCommandLogAsync(command, commandTimestamp, commandData, CancellationToken.None);

    /// <summary>Insert Command Log using the event-source persistence services.</summary>
    /// <param name="command">The command being audited or committed.</param>
    /// <param name="commandTimestamp">The command audit timestamp.</param>
    /// <param name="commandData">The serialized command audit payload.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    public async Task InsertCommandLogAsync(
        ICommand command,
        DateTime commandTimestamp,
        string commandData,
        CancellationToken cancellationToken)
    {
        _ = await TryInsertCommandLogAsync(command, commandTimestamp, commandData, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Try Insert Command Log using the event-source persistence services.</summary>
    /// <param name="command">The command being audited or committed.</param>
    /// <param name="commandTimestamp">The command audit timestamp.</param>
    /// <param name="commandData">The serialized command audit payload.</param>
    /// <returns>The typed result of the event-source operation.</returns>
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

    /// <summary>Try Insert Command Log using the event-source persistence services.</summary>
    /// <param name="command">The command being audited or committed.</param>
    /// <param name="commandTimestamp">The command audit timestamp.</param>
    /// <param name="commandData">The serialized command audit payload.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    public async Task<bool> TryInsertCommandLogAsync(
        ICommand command,
        DateTime commandTimestamp,
        string commandData,
        CancellationToken cancellationToken)
    {
        return await this.InsertCommandLogCoreAsync(command, commandTimestamp, commandData, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Insert Event Projector State using the event-source persistence services.</summary>
    /// <param name="state">The projector state to persist.</param>
    /// <returns>The completion of the persistence operation.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task InsertEventProjectorStateAsync(EventProjectorStateReadModel state)
        => await InsertEventProjectorStateAsync(state, CancellationToken.None)
            .ConfigureAwait(false);

    /// <summary>Insert Event Projector State using the event-source persistence services.</summary>
    /// <param name="state">The projector state to persist.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3236", Justification = "The validated member belongs to this public input argument; explicitly report its root parameter rather than exposing a member expression as the parameter name.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task InsertEventProjectorStateAsync(
        EventProjectorStateReadModel state,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var createdTimestamp = state.CreatedTimestamp == default ? now : state.CreatedTimestamp;
        var preparedParameters1 = new UpsertEventProjectorState(
                eventId: state.EventId,
                actorName: state.ActorName,
                projectorName: state.ProjectorName,
                isReplay: state.IsReplay,
                attemptNumber: state.AttemptNumber,
                outcome: $"{state.Outcome}",
                stage: $"{state.Stage}",
                errorMessage: state.ErrorMessage ?? string.Empty,
                createdTimestamp: $"{createdTimestamp:o}",
                updatedTimestamp: $"{now:o}");
        await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.UpsertEventProjectorState)}", this.EventLogSql(EventSourceDbSql.UpsertEventProjectorState))
            .SetParameters(preparedParameters1)
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets durable projection state for one event and one projector.
    /// </summary>
    /// <param name="eventId">The source event identifier.</param>
    /// <param name="projectorName">The projector identity.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    public async Task<EventProjectorStateReadModel?> GetEventProjectorStateAsync(
        long eventId,
        string projectorName)
        => await GetEventProjectorStateAsync(
            eventId,
            projectorName,
            CancellationToken.None)
            .ConfigureAwait(false);

    /// <summary>Get Event Projector State using the event-source persistence services.</summary>
    /// <param name="eventId">The source event identifier.</param>
    /// <param name="projectorName">The projector identity.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    public async Task<EventProjectorStateReadModel?> GetEventProjectorStateAsync(
        long eventId,
        string projectorName,
        CancellationToken cancellationToken)
    {
        var preparedParameters2 = new GetEventProjectorState(eventId, projectorName);
        return await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventProjectorState)}", EventSourceDbSql.GetEventProjectorState)
            .SetParameters(preparedParameters2)
            .ExecuteSingleAsync<EventProjectorStateReadModel>(MapToEventProjectorState, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Get Event Log By Event Id using the event-source persistence services.</summary>
    /// <param name="eventId">The source event identifier.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    public async Task<EventLogReadModel?> GetEventLogByEventIdAsync(
        long eventId,
        CancellationToken cancellationToken = default)
    {
        var preparedParameters3 = new GetEventLogByEventVersion(eventId);
        return await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventLogByEventVersion)}", this.EventLogSql(EventSourceDbSql.GetEventLogByEventVersion))
            .SetParameters(preparedParameters3)
            .ExecuteSingleAsync<EventLogReadModel>(MapToEventLog, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Try Create Event Projector Execution State using the event-source persistence services.</summary>
    /// <param name="state">The projector state to persist.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    public async Task<EventProjectorExecutionStateReadModel?> TryCreateEventProjectorExecutionStateAsync(
        EventProjectorExecutionStateReadModel state,
        CancellationToken cancellationToken = default)
    {
        var createdTimestamp = state.CreatedTimestamp;
        var updatedAtUtc = state.UpdatedAtUtc;
        var preparedParameters4 = new TryCreateEventProjectorExecutionState(
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
                updatedAtUtc);

        return await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.TryCreateEventProjectorExecutionState)}", this.EventLogSql(EventSourceDbSql.TryCreateEventProjectorExecutionState))
            .SetParameters(preparedParameters4)
            .ExecuteSingleAsync<EventProjectorExecutionStateReadModel>(MapToEventProjectorExecutionState, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Get Event Projector Execution State using the event-source persistence services.</summary>
    /// <param name="identity">The caller-qualified projector and event identity.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    public async Task<EventProjectorExecutionStateReadModel?> GetEventProjectorExecutionStateAsync(
        EventProjectorIdentity identity,
        CancellationToken cancellationToken = default)
    {
        var preparedParameters5 = new GetEventProjectorState(identity.EventId, identity.ProjectorName);
        return await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventProjectorExecutionState)}", EventSourceDbSql.GetEventProjectorExecutionState)
            .SetParameters(preparedParameters5)
            .ExecuteSingleAsync<EventProjectorExecutionStateReadModel>(MapToEventProjectorExecutionState, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Get Event Projector Stream Checkpoint using the event-source persistence services.</summary>
    /// <param name="projectorName">The projector identity.</param>
    /// <param name="eventStreamId">The event stream identifier.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    public async Task<EventProjectorStreamCheckpointReadModel?> GetEventProjectorStreamCheckpointAsync(
        string projectorName,
        long eventStreamId,
        CancellationToken cancellationToken = default)
    {
        var preparedParameters6 = new GetEventProjectorStreamCheckpoint(projectorName, eventStreamId);
        return await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventProjectorStreamCheckpoint)}", EventSourceDbSql.GetEventProjectorStreamCheckpoint)
            .SetParameters(preparedParameters6)
            .ExecuteSingleAsync<EventProjectorStreamCheckpointReadModel>(MapToEventProjectorStreamCheckpoint, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Try Claim Event Projector Execution using the event-source persistence services.</summary>
    /// <param name="lease">The execution lease prepared by the caller.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    public async Task<EventProjectorExecutionStateReadModel?> TryClaimEventProjectorExecutionAsync(
        EventProjectorLease lease,
        CancellationToken cancellationToken = default)
    {
        var parameters = new TryClaimEventProjectorExecution(
            lease.EventId, lease.ProjectorName, lease.ExecutionToken,
            lease.ExpiresAtUtc, lease.NowUtc, $"{lease.NowUtc:o}");
        return await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.TryClaimEventProjectorExecution)}", EventSourceDbSql.TryClaimEventProjectorExecution)
            .SetParameters(parameters)
            .ExecuteSingleAsync<EventProjectorExecutionStateReadModel>(MapToEventProjectorExecutionState, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Has Earlier Unresolved Event Projector Execution using the event-source persistence services.</summary>
    /// <param name="identity">The caller-qualified projector and event identity.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    public async Task<bool> HasEarlierUnresolvedEventProjectorExecutionAsync(
        EventProjectorIdentity identity,
        CancellationToken cancellationToken = default)
    {
        var preparedParameters8 = new GetEventProjectorState(identity.EventId, identity.ProjectorName);
        return await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.HasEarlierUnresolvedEventProjectorExecution)}", EventSourceDbSql.HasEarlierUnresolvedEventProjectorExecution)
            .SetParameters(preparedParameters8)
            .ExecuteScalarAsync(MapToHasEarlierUnresolvedEventProjectorExecutionRow1, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Try Renew Event Projector Execution using the event-source persistence services.</summary>
    /// <param name="lease">The execution lease prepared by the caller.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    public async Task<EventProjectorExecutionStateReadModel?> TryRenewEventProjectorExecutionAsync(
        EventProjectorLease lease,
        CancellationToken cancellationToken = default)
    {
        var parameters = new TryRenewEventProjectorExecution(
            lease.EventId, lease.ProjectorName, lease.ExecutionToken,
            lease.ExpectedRevision, lease.NowUtc, lease.ExpiresAtUtc, $"{lease.NowUtc:o}");
        return await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.TryRenewEventProjectorExecution)}", EventSourceDbSql.TryRenewEventProjectorExecution)
            .SetParameters(parameters)
            .ExecuteSingleAsync<EventProjectorExecutionStateReadModel>(MapToEventProjectorExecutionState, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Try Release Event Projector Execution using the event-source persistence services.</summary>
    /// <param name="transition">The requested revision and lease-qualified projector transition.</param>
    /// <param name="nowUtc">The current UTC timestamp.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    public async Task<EventProjectorExecutionStateReadModel?> TryReleaseEventProjectorExecutionAsync(
        EventProjectorStateTransition transition,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var preparedParameters10 = new TryReleaseEventProjectorExecution(
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
                $"{nowUtc:o}");
        return await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.TryReleaseEventProjectorExecution)}", EventSourceDbSql.TryReleaseEventProjectorExecution)
            .SetParameters(preparedParameters10)
            .ExecuteSingleAsync<EventProjectorExecutionStateReadModel>(MapToEventProjectorExecutionState, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Try Transition Event Projector Execution using the event-source persistence services.</summary>
    /// <param name="transition">The requested revision and lease-qualified projector transition.</param>
    /// <param name="nowUtc">The current UTC timestamp.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    public async Task<EventProjectorExecutionStateReadModel?> TryTransitionEventProjectorExecutionAsync(
        EventProjectorStateTransition transition,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var preparedParameters11 = new TryTransitionEventProjectorExecution(
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
                transition.NextAttemptAtUtc,
                transition.LastErrorAtUtc,
                transition.ErrorMessage ?? string.Empty,
                transition.BlockedReason ?? string.Empty,
                nowUtc,
                $"{nowUtc:o}");
        return await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.TryTransitionEventProjectorExecution)}", EventSourceDbSql.TryTransitionEventProjectorExecution)
            .SetParameters(preparedParameters11)
            .ExecuteSingleAsync<EventProjectorExecutionStateReadModel>(MapToEventProjectorExecutionState, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Try Terminalize Event Projector Execution using the event-source persistence services.</summary>
    /// <param name="transition">The requested revision and lease-qualified projector transition.</param>
    /// <param name="nowUtc">The current UTC timestamp.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    public async Task<EventProjectorExecutionStateReadModel?> TryTerminalizeEventProjectorExecutionAsync(
        EventProjectorStateTransition transition,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var preparedParameters12 = new TryTerminalizeEventProjectorExecution(
                transition.EventId,
                transition.ProjectorName,
                transition.ExecutionToken,
                transition.ExpectedRevision,
                $"{transition.ExpectedStage}",
                nowUtc,
                $"{transition.Outcome}",
                $"{transition.LastCompletedStage}",
                transition.RetryCount,
                transition.LastErrorAtUtc,
                transition.ErrorMessage ?? string.Empty,
                transition.BlockedReason ?? string.Empty,
                nowUtc,
                $"{nowUtc:o}");
        return await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.TryTerminalizeEventProjectorExecution)}", EventSourceDbSql.TryTerminalizeEventProjectorExecution)
            .SetParameters(preparedParameters12)
            .ExecuteSingleAsync<EventProjectorExecutionStateReadModel>(MapToEventProjectorExecutionState, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Try Transition Event Projector Execution With Outbox using the event-source persistence services.</summary>
    /// <param name="transition">The requested revision and lease-qualified projector transition.</param>
    /// <param name="message">The persisted outbox message and its dispatch ownership.</param>
    /// <param name="nowUtc">The current UTC timestamp.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    public async Task<EventProjectorExecutionStateReadModel?> TryTransitionEventProjectorExecutionWithOutboxAsync(
        EventProjectorStateTransition transition,
        EventProjectorOutboxMessage message,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
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
            transition.NextAttemptAtUtc,
            transition.LastErrorAtUtc,
            transition.ErrorMessage ?? string.Empty,
            transition.BlockedReason ?? string.Empty,
            nowUtc,
            $"{nowUtc:o}");
        var preparedParameters13 = new TryTransitionEventProjectorExecutionWithOutbox(
                parameters,
                $"{message.Identity.EffectKind}",
                message.MessageId,
                message.EventTypeName,
                message.EventPayload,
                nowUtc,
                MessageOriginJson.Serialize(message.Origin));
        return await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.TryTransitionEventProjectorExecutionWithOutbox)}", EventSourceDbSql.TryTransitionEventProjectorExecutionWithOutbox)
            .SetParameters(preparedParameters13)
            .ExecuteSingleAsync<EventProjectorExecutionStateReadModel>(MapToEventProjectorExecutionState, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Try Terminalize Event Projector Execution With Outbox using the event-source persistence services.</summary>
    /// <param name="transition">The requested revision and lease-qualified projector transition.</param>
    /// <param name="message">The persisted outbox message and its dispatch ownership.</param>
    /// <param name="nowUtc">The current UTC timestamp.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    public async Task<EventProjectorExecutionStateReadModel?> TryTerminalizeEventProjectorExecutionWithOutboxAsync(
        EventProjectorStateTransition transition,
        EventProjectorOutboxMessage message,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
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
            transition.LastErrorAtUtc,
            transition.ErrorMessage ?? string.Empty,
            transition.BlockedReason ?? string.Empty,
            nowUtc,
            $"{nowUtc:o}");
        var preparedParameters14 = new TryTerminalizeEventProjectorExecutionWithOutbox(
                parameters,
                $"{message.Identity.EffectKind}",
                message.MessageId,
                message.EventTypeName,
                message.EventPayload,
                nowUtc,
                MessageOriginJson.Serialize(message.Origin));
        return await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.TryTerminalizeEventProjectorExecutionWithOutbox)}", EventSourceDbSql.TryTerminalizeEventProjectorExecutionWithOutbox)
            .SetParameters(preparedParameters14)
            .ExecuteSingleAsync<EventProjectorExecutionStateReadModel>(MapToEventProjectorExecutionState, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Claim Event Projector Outbox using the event-source persistence services.</summary>
    /// <param name="projectorName">The projector identity.</param>
    /// <param name="dispatchToken">The token identifying the current outbox lease owner.</param>
    /// <param name="nowUtc">The current UTC timestamp.</param>
    /// <param name="leaseDuration">The requested lease duration.</param>
    /// <param name="batchSize">The maximum number of records to return or claim.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    public async Task<IReadOnlyList<EventProjectorOutboxReadModel>> ClaimEventProjectorOutboxAsync(
        string projectorName,
        Guid dispatchToken,
        DateTime nowUtc,
        TimeSpan leaseDuration,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var leaseExpiresAtUtc = nowUtc.Add(leaseDuration);
        var preparedParameters15 = new ClaimEventProjectorOutbox(
                projectorName,
                dispatchToken,
                leaseExpiresAtUtc,
                nowUtc,
                batchSize);
        return [.. await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.ClaimEventProjectorOutbox)}", EventSourceDbSql.ClaimEventProjectorOutbox)
            .SetParameters(preparedParameters15)
            .ExecuteQueryAsync<EventProjectorOutboxReadModel>(MapToEventProjectorOutbox, cancellationToken)
            .ConfigureAwait(false)];
    }

    /// <summary>Mark Event Projector Outbox Published using the event-source persistence services.</summary>
    /// <param name="message">The persisted outbox message and its dispatch ownership.</param>
    /// <param name="nowUtc">The current UTC timestamp.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    public async Task<bool> MarkEventProjectorOutboxPublishedAsync(
        EventProjectorOutboxReadModel message,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var preparedParameters16 = new MarkEventProjectorOutboxPublished(
                message.ProjectorName,
                message.EventId,
                $"{message.EffectKind}",
                message.DispatchToken,
                nowUtc,
                nowUtc);
        var affected = await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.MarkEventProjectorOutboxPublished)}", EventSourceDbSql.MarkEventProjectorOutboxPublished)
            .SetParameters(preparedParameters16)
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
        return affected.Sum() == 1;
    }

    /// <summary>Release Event Projector Outbox using the event-source persistence services.</summary>
    /// <param name="message">The persisted outbox message and its dispatch ownership.</param>
    /// <param name="status">The requested projector or outbox status.</param>
    /// <param name="nextAttemptAtUtc">The next eligible retry time in UTC.</param>
    /// <param name="lastError">The diagnostic error from the failed attempt.</param>
    /// <param name="nowUtc">The current UTC timestamp.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    public async Task<bool> ReleaseEventProjectorOutboxAsync(
        EventProjectorOutboxReadModel message,
        EventProjectorOutboxStatus status,
        DateTime? nextAttemptAtUtc,
        string lastError,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var preparedParameters17 = new ReleaseEventProjectorOutbox(
                message.ProjectorName,
                message.EventId,
                $"{message.EffectKind}",
                message.DispatchToken,
                nowUtc,
                $"{status}",
                nextAttemptAtUtc,
                lastError ?? string.Empty);
        var affected = await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.ReleaseEventProjectorOutbox)}", EventSourceDbSql.ReleaseEventProjectorOutbox)
            .SetParameters(preparedParameters17)
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
        return affected.Sum() == 1;
    }

    /// <summary>Get Event Projector Operational State Page using the event-source persistence services.</summary>
    /// <param name="projectorName">The projector identity.</param>
    /// <param name="status">The requested projector or outbox status.</param>
    /// <param name="afterEventId">The exclusive event identifier page cursor.</param>
    /// <param name="batchSize">The maximum number of records to return or claim.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    public async Task<IReadOnlyList<EventProjectorExecutionStateReadModel>> GetEventProjectorOperationalStatePageAsync(
        string projectorName,
        EventProjectorOperationalStatus status,
        long afterEventId,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var preparedParameters18 = new GetEventProjectorOperationalStatePage(
                projectorName, $"{status}", afterEventId, batchSize);
        return [.. await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventProjectorOperationalStatePage)}", this.EventLogSql(EventSourceDbSql.GetEventProjectorOperationalStatePage))
            .SetParameters(preparedParameters18)
            .ExecuteQueryAsync<EventProjectorExecutionStateReadModel>(MapToEventProjectorExecutionState, cancellationToken)
            .ConfigureAwait(false)];
    }

    /// <summary>Get Event Projector Operational Snapshot using the event-source persistence services.</summary>
    /// <param name="projectorName">The projector identity.</param>
    /// <param name="nowUtc">The current UTC timestamp.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    public async Task<EventProjectorOperationalSnapshotReadModel> GetEventProjectorOperationalSnapshotAsync(
        string projectorName,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var preparedParameters19 = new GetEventProjectorOperationalSnapshot(projectorName, nowUtc);
        return await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventProjectorOperationalSnapshot)}", this.EventLogSql(EventSourceDbSql.GetEventProjectorOperationalSnapshot))
            .SetParameters(preparedParameters19)
            .ExecuteSingleAsync<EventProjectorOperationalSnapshotReadModel>(MapToEventProjectorOperationalSnapshot, cancellationToken)
            .ConfigureAwait(false)
            ?? new EventProjectorOperationalSnapshotReadModel(0, null, 0, 0, 0, 0, null, 0);
    }

    /// <summary>Try Retry Event Projector Execution using the event-source persistence services.</summary>
    /// <param name="eventId">The source event identifier.</param>
    /// <param name="projectorName">The projector identity.</param>
    /// <param name="nowUtc">The current UTC timestamp.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    public async Task<EventProjectorExecutionStateReadModel?> TryRetryEventProjectorExecutionAsync(
        long eventId,
        string projectorName,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var preparedParameters20 = new RetryEventProjectorExecution(
                eventId, projectorName, nowUtc, $"{nowUtc:o}");
        return await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.TryRetryEventProjectorExecution)}", EventSourceDbSql.TryRetryEventProjectorExecution)
            .SetParameters(preparedParameters20)
            .ExecuteSingleAsync<EventProjectorExecutionStateReadModel>(MapToEventProjectorExecutionState, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Try Skip Event Projector Execution using the event-source persistence services.</summary>
    /// <param name="eventId">The source event identifier.</param>
    /// <param name="projectorName">The projector identity.</param>
    /// <param name="reason">The operator supplied reason for skipping the event.</param>
    /// <param name="nowUtc">The current UTC timestamp.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    public async Task<EventProjectorExecutionStateReadModel?> TrySkipEventProjectorExecutionAsync(
        long eventId,
        string projectorName,
        string reason,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var preparedParameters21 = new SkipEventProjectorExecution(
                eventId, projectorName, reason, nowUtc, $"{nowUtc:o}");
        return await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.TrySkipEventProjectorExecution)}", EventSourceDbSql.TrySkipEventProjectorExecution)
            .SetParameters(preparedParameters21)
            .ExecuteSingleAsync<EventProjectorExecutionStateReadModel>(MapToEventProjectorExecutionState, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Get Event Projector Recovery Page using the event-source persistence services.</summary>
    /// <param name="projectorName">The projector identity.</param>
    /// <param name="eventNames">The event names to include.</param>
    /// <param name="afterEventId">The exclusive event identifier page cursor.</param>
    /// <param name="nowUtc">The current UTC timestamp.</param>
    /// <param name="batchSize">The maximum number of records to return or claim.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    public async Task<IReadOnlyList<EventProjectorRecoveryItemReadModel>> GetEventProjectorRecoveryPageAsync(
        string projectorName,
        IReadOnlyCollection<string> eventNames,
        long afterEventId,
        DateTime nowUtc,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        if (eventNames.Count == 0)
            return [];
        var preparedParameters22 = new GetEventProjectorRecoveryPage(
                projectorName,
                string.Join(',', eventNames),
                afterEventId,
                nowUtc,
                batchSize);

        return [.. await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventProjectorRecoveryPage)}", this.EventLogSql(EventSourceDbSql.GetEventProjectorRecoveryPage))
            .SetParameters(preparedParameters22)
            .ExecuteQueryAsync<EventProjectorRecoveryItemReadModel>(MapToEventProjectorRecoveryItem, cancellationToken)
            .ConfigureAwait(false)];
    }

    /// <summary>
    /// Gets event-log entries that the named projector supports and has not terminally processed.
    /// </summary>
    /// <param name="projectorName">The projector identity.</param>
    /// <param name="eventNames">The event names to include.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    public async Task<ICollection<EventLogReadModel>> GetUncompletedEventProjectorEventsAsync(
        string projectorName,
        IReadOnlyCollection<string> eventNames)
        => await GetUncompletedEventProjectorEventsAsync(
            projectorName,
            eventNames,
            CancellationToken.None)
            .ConfigureAwait(false);

    /// <summary>Get Uncompleted Event Projector Events using the event-source persistence services.</summary>
    /// <param name="projectorName">The projector identity.</param>
    /// <param name="eventNames">The event names to include.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    public async Task<ICollection<EventLogReadModel>> GetUncompletedEventProjectorEventsAsync(
        string projectorName,
        IReadOnlyCollection<string> eventNames,
        CancellationToken cancellationToken)
    {
        if (eventNames.Count == 0)
            return [];
        var preparedParameters23 = new GetUncompletedEventProjectorEvents(
                projectorName,
                string.Join(',', eventNames));

        return await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetUncompletedEventProjectorEvents)}", this.EventLogSql(EventSourceDbSql.GetUncompletedEventProjectorEvents))
            .SetParameters(preparedParameters23)
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
        => await UpdateCommandLogAsync(commandId, updateTimestamp, commandStatus, CancellationToken.None)
            .ConfigureAwait(false);

    /// <summary>Update Command Log using the event-source persistence services.</summary>
    /// <param name="commandId">The originating command identifier.</param>
    /// <param name="updateTimestamp">The command status update timestamp.</param>
    /// <param name="commandStatus">The new command audit status.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The completion of the persistence operation.</returns>
    public async Task UpdateCommandLogAsync(
        Guid commandId,
        DateTime updateTimestamp,
        CommandStatus commandStatus,
        CancellationToken cancellationToken)
    {
        var parameters = new UpdateCommandLog(commandId: commandId, commandStatus: $"{commandStatus}", updateTimestamp: updateTimestamp);
        await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.UpdateCommandLog)}", EventSourceDbSql.UpdateCommandLog)
            .SetParameters(parameters)
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

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
            .ExecuteCommandAsync())
            .ConfigureAwait(false);

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
            await (DeleteEventLogAsync(eventVersion))
            .ConfigureAwait(false);
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
            .ExecuteCommandAsync())
            .ConfigureAwait(false);

    /// <summary>
    /// Asynchronously deletes the event stream with the specified identifier from the data store.
    /// </summary>
    /// <param name="eventStreamId">The unique identifier of the event stream to delete.</param>
    /// <returns>A task that represents the asynchronous delete operation.</returns>
    public async Task DeleteEventStreamByIdAsync(long eventStreamId)
        => await (_dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.DeleteEventStreamById)}", EventSourceDbSql.DeleteEventStreamById)
            .SetParameters(new DeleteEventStreamById(eventStreamId))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);

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
            .ExecuteSingleAsync<CommandLogReadModel>(MapToCommandLog))
            .ConfigureAwait(false);

    /// <summary>Has Event For Command using the event-source persistence services.</summary>
    /// <param name="commandId">The originating command identifier.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    public async Task<bool> HasEventForCommandAsync(Guid commandId)
        => await (_dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.HasEventForCommand)}", this.EventLogSql(EventSourceDbSql.HasEventForCommand))
            .SetParameters(new GetCommandLog(commandId))
            .ExecuteScalarAsync(MapToHasEventForCommandRow2))
            .ConfigureAwait(false);

    /// <summary>Reads the last committed source version; filtered state windows do not define stream concurrency.</summary>
    /// <param name="eventStreamId">The event stream identifier.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    public async Task<long> GetCommittedStreamVersionAsync(long eventStreamId, CancellationToken cancellationToken = default)
    {
        var preparedParameters24 = new GetEventLogByEventStreamId(eventStreamId);
        return await Database.Use("EventSource.StreamVersion", this.EventLogSql(EventSourceDbSql.EventSourceActorDbContext_GetCommittedStreamVersionAsync_1))
            .SetParameters(preparedParameters24)
            .ExecuteSingleAsync(MapToGetCommittedStreamVersionRow3, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Reads the current stream head directly for optimistic resident no-change/replay validation.</summary>
    /// <param name="eventStream">The exact event stream name.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    public Task<long> GetCommittedStreamVersionAsync(string eventStream, CancellationToken cancellationToken = default)
        => this.ReadCommittedStreamHeadAsync(eventStream, cancellationToken);

    /// <summary>Get Event Stream Id From Db using the event-source persistence services.</summary>
    /// <param name="eventStream">The event stream name.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    public async Task<EventStreamIdReadModel?> GetEventStreamIdFromDbAsync(string eventStream)
        => await (_dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventStreamId)}", EventSourceDbSql.GetEventStreamId)
            .SetParameters(new GetEventStreamId(eventStream))
            .ExecuteSingleAsync<EventStreamIdReadModel>(MapToEventStreamId))
            .ConfigureAwait(false);

    /// <summary>
    /// Retrieves the unique identifier associated with the event name of the specified domain event type.
    /// </summary>
    /// <typeparam name="TEvent">The type of the domain event, which must implement <see cref="IEvent"/>.</typeparam>
    /// <param name="domainEvent">The domain event instance whose event name identifier is to be retrieved. Cannot be <see langword="null"/>.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the unique identifier for the event
    /// name associated with the specified domain event type.</returns>
    public async Task<int> GetEventNameIdFromDomainEventAsync<TEvent>(TEvent domainEvent) where TEvent : IEvent
        => await GetEventNameIdFromDomainEventAsync(domainEvent, CancellationToken.None)
            .ConfigureAwait(false);

    /// <summary>Get Event Name Id From Domain Event using the event-source persistence services.</summary>
    /// <param name="domainEvent">The domain event used by this operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <typeparam name="TEvent">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the event-source operation.</returns>
    public async Task<int> GetEventNameIdFromDomainEventAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken) where TEvent : IEvent
        => await this.GetEventNameIdFromTypeAsync(domainEvent.GetType(), cancellationToken)
            .ConfigureAwait(false);

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
        var snapshotEventNameId = await this.GetCompatibleEventNameIdsAsync<TSnapshot>(CancellationToken.None)
            .ConfigureAwait(false);
        var db = _dbFactory.ActorEventSourceDb;
        var preparedParameters25 = new GetMaxEventVersion(eventStreamId, snapshotEventNameId);
        var maxEventVersion = await (db.Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetMaxEventVersion)}", this.EventLogSql(EventSourceDbSql.GetMaxEventVersion))
            .SetParameters(preparedParameters25)
            .ExecuteScalarAsync(MapToLong))
            .ConfigureAwait(false);
        var preparedParameters26 = new GetEventLogByMaxEventVersion(eventStreamId, maxEventVersion);
        return maxEventVersion > 0
            ? await (db.Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventLogByMaxEventVersion)}", this.EventLogSql(EventSourceDbSql.GetEventLogByMaxEventVersion))
            .SetParameters(preparedParameters26)
            .ExecuteQueryAsync<EventStreamReadModel>(MapToEventStream))
            .ConfigureAwait(false)
            : await (db.Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventLogByEventStreamId)}", this.EventLogSql(EventSourceDbSql.GetEventLogByEventStreamId))
            .SetParameters(new GetEventLogByEventStreamId(eventStreamId))
            .ExecuteQueryAsync<EventStreamReadModel>(MapToEventStream))
            .ConfigureAwait(false);
    }

    /// <summary>Restores creation plus tail without observing an append beyond the captured head.</summary>
    /// <param name="eventStreamId">The numeric identity of the resolved event stream.</param>
    /// <param name="reducerAction">The reducer action supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    public async ValueTask MapReduceActorEventStreamFromCreationAsync<TState>(
        long eventStreamId,
        Action<IEnumerable<EventStreamReadModel>> reducerAction,
        CancellationToken cancellationToken = default)
        where TState : IActorState<TState>
        => await this.ReadCreationSnapshotAsync(eventStreamId, reducerAction, null, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>Reads latest independent replacement snapshots without mixing their committed append boundaries.</summary>
    /// <param name="eventStreamId">The numeric identity of the resolved event stream.</param>
    /// <param name="reducerAction">The reducer action supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    public async ValueTask MapReduceActorEventStreamFromLatestSnapshotsAsync<TState, TFirst, TSecond>(
        long eventStreamId, Action<IEnumerable<EventStreamReadModel>> reducerAction,
        CancellationToken cancellationToken = default)
        where TState : IActorState<TState>
        where TFirst : IEvent
        where TSecond : IEvent
    {
        var first = await this.GetCompatibleEventNameIdsAsync<TFirst>(cancellationToken)
            .ConfigureAwait(false);
        var second = await this.GetCompatibleEventNameIdsAsync<TSecond>(cancellationToken)
            .ConfigureAwait(false);
        await this.ReadCreationSnapshotAsync(eventStreamId, reducerAction, first.Concat(second).Distinct().ToArray(),
            cancellationToken, components: true)
            .ConfigureAwait(false);
    }

    /// <summary>Uses known persisted identities in the checkpoint read itself.</summary>
    /// <param name="eventStreamId">The numeric identity of the resolved event stream.</param>
    /// <param name="checkpointTypes">The checkpoint types supplied by the caller.</param>
    /// <param name="reducerAction">The reducer action supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    public async ValueTask MapReduceActorEventStreamFromCreationWithCheckpointsAsync<TState>(
        long eventStreamId, IReadOnlyList<Type> checkpointTypes,
        Action<IEnumerable<EventStreamReadModel>> reducerAction, CancellationToken cancellationToken = default)
        where TState : IActorState<TState>
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identities = checkpointTypes.SelectMany(EventTypeRegistry.Default.GetReadIdentities)
            .Distinct(StringComparer.Ordinal).ToArray();
        // A resolvable older spelling outside the registered identities is replayed in the tail
        // until a known checkpoint supersedes it.
        await this.ReadCreationSnapshotAsync(eventStreamId, reducerAction, null, cancellationToken,
            checkpointIdentities: identities)
            .ConfigureAwait(false);
    }

    /// <summary>Uses a full checkpoint only when its owning repository explicitly declares that optimization.</summary>
    /// <param name="eventStreamId">The numeric identity of the resolved event stream.</param>
    /// <param name="reducerAction">The reducer action supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    public async ValueTask MapReduceActorEventStreamFromCreationWithCheckpointAsync<TState, TCheckpoint>(
        long eventStreamId, Action<IEnumerable<EventStreamReadModel>> reducerAction,
        CancellationToken cancellationToken = default)
        where TState : IActorState<TState>
        where TCheckpoint : IEvent
    {
        var names = await this.GetCompatibleEventNameIdsAsync<TCheckpoint>(cancellationToken)
            .ConfigureAwait(false);
        await this.ReadCreationSnapshotAsync(eventStreamId, reducerAction, names, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Reads only creation/head when the state contract declares no retained business fields.</summary>
    /// <param name="eventStreamId">The numeric identity of the resolved event stream.</param>
    /// <param name="reducerAction">The reducer action supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    public async ValueTask MapReduceStatelessActorEventStreamFromCreationAsync<TState>(
        long eventStreamId, Action<IEnumerable<EventStreamReadModel>> reducerAction,
        CancellationToken cancellationToken = default)
        where TState : IActorState<TState>, IStatelessCreationSnapshotState
        => await this.ReadCreationSnapshotAsync(eventStreamId, reducerAction, null, cancellationToken, true)
            .ConfigureAwait(false);


    /// <summary>
    /// Processes the full event stream by mapping records and invoking the provided reducer action.
    /// </summary>
    /// <typeparam name="TState">The actor state type that implements <see cref="IActorState{TState}"/>.</typeparam>
    /// <param name="eventStreamId">The unique identifier of the event stream to process.</param>
    /// <param name="reducerAction">The action invoked with the mapped <see cref="EventStreamReadModel"/> sequence.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    public async ValueTask MapReduceActorEventStreamAsync<TState>(long eventStreamId, Action<IEnumerable<EventStreamReadModel>> reducerAction) where TState : IActorState<TState>
        => await MapReduceActorEventStreamAsync<TState>(eventStreamId, reducerAction, CancellationToken.None)
            .ConfigureAwait(false);

    /// <summary>Map Reduce Actor Event Stream using the event-source persistence services.</summary>
    /// <param name="eventStreamId">The event stream identifier.</param>
    /// <param name="reducerAction">The reducer applied to the loaded stream events.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <typeparam name="TState">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The completion of the persistence operation.</returns>
    public async ValueTask MapReduceActorEventStreamAsync<TState>(
        long eventStreamId,
        Action<IEnumerable<EventStreamReadModel>> reducerAction,
        CancellationToken cancellationToken)
        where TState : IActorState<TState>
    {
        var eventStream = new EventStreamReadModel();
        var preparedParameters28 = new GetEventLogByEventStreamId(eventStreamId);
        await (_dbFactory.ActorEventSourceDb
                .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventLogByEventStreamId)}", this.EventLogSql(EventSourceDbSql.GetEventLogByEventStreamId))
            .SetParameters(preparedParameters28)
            .ExecuteMapReduceAsync(EventStreamMapper, reducerAction, cancellationToken))
            .ConfigureAwait(false);

        EventStreamReadModel EventStreamMapper(IObjectDataRecord o)
        {
            eventStream.EventVersion = o.GetLong(3);
            eventStream.EventTypeName = o.GetString(2);
            eventStream.EventData = o.GetBytes(4);
            eventStream.Origin = MapToOrigin(o);
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
        => await MapReduceActorEventStreamAsync<TState, TEvent>(eventStreamId, lastNRange, reducerAction, CancellationToken.None)
            .ConfigureAwait(false);

    /// <summary>Map Reduce Actor Event Stream using the event-source persistence services.</summary>
    /// <param name="eventStreamId">The event stream identifier.</param>
    /// <param name="lastNRange">The number of recent matching events to load.</param>
    /// <param name="reducerAction">The reducer applied to the loaded stream events.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <typeparam name="TState">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <typeparam name="TEvent">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The completion of the persistence operation.</returns>
    public async ValueTask MapReduceActorEventStreamAsync<TState, TEvent>(
        long eventStreamId,
        int lastNRange,
        Action<IEnumerable<EventStreamReadModel>> reducerAction,
        CancellationToken cancellationToken)
        where TState : IActorState<TState>
        where TEvent : IEvent
    {
        var eventNameId = await this.GetCompatibleEventNameIdsAsync<TEvent>(cancellationToken)
            .ConfigureAwait(false);
        var preparedParameters29 = new GetEventLogLastNRangeByEventName(
                eventStreamId,
                eventNameId,
                Math.Max(0, lastNRange));
        await (_dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventLogLastNRangeByEventName)}", this.EventLogSql(EventSourceDbSql.GetEventLogLastNRangeByEventName))
            .SetParameters(preparedParameters29)
            .ExecuteMapReduceAsync(MapToEventStream, reducerAction, cancellationToken))
            .ConfigureAwait(false);
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
        => await MapReduceActorEventStreamAsync<TState, TSnapshot>(eventStreamId, reducerAction, CancellationToken.None)
            .ConfigureAwait(false);

    /// <summary>Map Reduce Actor Event Stream using the event-source persistence services.</summary>
    /// <param name="eventStreamId">The event stream identifier.</param>
    /// <param name="reducerAction">The reducer applied to the loaded stream events.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <typeparam name="TState">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <typeparam name="TSnapshot">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The completion of the persistence operation.</returns>
    public async ValueTask MapReduceActorEventStreamAsync<TState, TSnapshot>(
        long eventStreamId,
        Action<IEnumerable<EventStreamReadModel>> reducerAction,
        CancellationToken cancellationToken)
        where TState : IActorState<TState>
        where TSnapshot : IEvent
    {
        var eventStream = new EventStreamReadModel();
        var snapshotEventNameId = await this.GetCompatibleEventNameIdsAsync<TSnapshot>(cancellationToken)
            .ConfigureAwait(false);
        var db = _dbFactory.ActorEventSourceDb;
        var preparedParameters30 = new GetMaxEventVersion(eventStreamId, snapshotEventNameId);
        var maxEventVersion = await (db.Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetMaxEventVersion)}", this.EventLogSql(EventSourceDbSql.GetMaxEventVersion))
            .SetParameters(preparedParameters30)
            .ExecuteScalarAsync(MapToLong, cancellationToken))
            .ConfigureAwait(false);
        if (maxEventVersion > 0)
            await (db.Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventLogByMaxEventVersion)}", this.EventLogSql(EventSourceDbSql.GetEventLogByMaxEventVersion))
            .SetParameters(new GetEventLogByMaxEventVersion(eventStreamId, maxEventVersion))
            .ExecuteMapReduceAsync(EventStreamMapper, reducerAction, cancellationToken))
            .ConfigureAwait(false);
        else
            await (db.Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventLogByEventStreamId)}", this.EventLogSql(EventSourceDbSql.GetEventLogByEventStreamId))
            .SetParameters(new GetEventLogByEventStreamId(eventStreamId))
            .ExecuteMapReduceAsync(EventStreamMapper, reducerAction, cancellationToken))
            .ConfigureAwait(false);

        EventStreamReadModel EventStreamMapper(IObjectDataRecord o)
        {
            eventStream.EventVersion = o.GetLong(3);
            eventStream.EventTypeName = o.GetString(2);
            eventStream.EventData = o.GetBytes(4);
            eventStream.Origin = MapToOrigin(o);
            eventStream.StreamVersion = o.GetLong(7);
            return eventStream;
        }
    }

    /// <summary>
    /// Replays the latest snapshot followed by the last N matching post-snapshot events.
    /// A missing snapshot produces an empty sequence. Results are streamed in ascending event-version order.
    /// </summary>
    /// <param name="eventStreamId">The event stream identifier.</param>
    /// <param name="lastNRange">The number of recent matching events to load.</param>
    /// <param name="reducerAction">The reducer applied to the loaded stream events.</param>
    /// <typeparam name="TState">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <typeparam name="TSnapshot">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <typeparam name="TRangeEvent">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The completion of the persistence operation.</returns>
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
            CancellationToken.None)
            .ConfigureAwait(false);

    /// <summary>Map Reduce Actor Event Stream From Snapshot Last NRange using the event-source persistence services.</summary>
    /// <param name="eventStreamId">The event stream identifier.</param>
    /// <param name="lastNRange">The number of recent matching events to load.</param>
    /// <param name="reducerAction">The reducer applied to the loaded stream events.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <typeparam name="TState">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <typeparam name="TSnapshot">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <typeparam name="TRangeEvent">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The completion of the persistence operation.</returns>
    public async ValueTask MapReduceActorEventStreamFromSnapshotLastNRangeAsync<TState, TSnapshot, TRangeEvent>(
        long eventStreamId,
        int lastNRange,
        Action<IEnumerable<EventStreamReadModel>> reducerAction,
        CancellationToken cancellationToken)
        where TState : IActorState<TState>
        where TSnapshot : IEvent
        where TRangeEvent : IEvent
    {
        var snapshotEventNameId = await this.GetCompatibleEventNameIdsAsync<TSnapshot>(cancellationToken)
            .ConfigureAwait(false);
        var rangeEventNameId = await this.GetCompatibleEventNameIdsAsync<TRangeEvent>(cancellationToken)
            .ConfigureAwait(false);
        var preparedParameters31 = new GetEventLogFromSnapshotLastNRange(
                eventStreamId,
                snapshotEventNameId,
                rangeEventNameId,
                Math.Max(0, lastNRange));
        await (_dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventLogFromSnapshotLastNRange)}", this.EventLogSql(EventSourceDbSql.GetEventLogFromSnapshotLastNRange))
            .SetParameters(preparedParameters31)
            .ExecuteMapReduceAsync(MapToEventStream, reducerAction, cancellationToken))
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Loads the full event stream into memory for the specified actor state type.
    /// </summary>
    /// <typeparam name="TState">The actor state type that implements <see cref="IActorState{TState}"/>.</typeparam>
    /// <param name="eventStreamId">The unique identifier of the event stream to load.</param>
    /// <returns>An <see cref="ICollection{T}"/> of <see cref="EventStreamReadModel"/> entries for the stream.</returns>
    public async ValueTask<ICollection<EventStreamReadModel>> LoadActorEventStreamAsync<TState>(long eventStreamId)
        where TState : IActorState<TState>
            => await (this.GetEventStreamAsync(eventStreamId))
            .ConfigureAwait(false);

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
        var eventNameId = await this.GetCompatibleEventNameIdsAsync<TEvent>(CancellationToken.None)
            .ConfigureAwait(false);
        var preparedParameters32 = new GetEventLogLastNRangeByEventName(
                eventStreamId,
                eventNameId,
                Math.Max(0, lastNRange));
        return await (_dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventLogLastNRangeByEventName)}", this.EventLogSql(EventSourceDbSql.GetEventLogLastNRangeByEventName))
            .SetParameters(preparedParameters32)
            .ExecuteQueryAsync<EventStreamReadModel>(MapToEventStream))
            .ConfigureAwait(false);
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
           => await (GetEventsFromSnapshotAsync<TSnapshot>(eventStreamId))
            .ConfigureAwait(false);


    /// <summary>Reads an aggregate head, its newest indexed sidecar checkpoint and the bounded source tail.</summary>
    /// <param name="stream">The exact event stream name.</param>
    /// <param name="sidecar">The sidecar supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <param name="through">The inclusive upper revision bound for the read.</param>
    /// <param name="maximumCheckpointRevision">The maximum checkpoint revision supplied by the caller.</param>
    /// <param name="replayAfter">The replay after supplied by the caller.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    public async Task<AggregateCheckpointWindow> ReadAggregateCheckpointWindowAsync(
        string stream, string sidecar, CancellationToken cancellationToken, long through = long.MaxValue,
        long maximumCheckpointRevision = long.MaxValue, long replayAfter = -1)
    {
        var preparedParameters33 = new AggregateWindowParameters(stream, sidecar, through, maximumCheckpointRevision, replayAfter);
        var rows = await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.ReadAggregateCheckpointWindow)}", EventSourceDbSql.ReadAggregateCheckpointWindow)
            .SetParameters(preparedParameters33)
            .ExecuteQueryAsync(MapToAggregateCheckpointRow, cancellationToken)
            .ConfigureAwait(false);
        return new AggregateCheckpointWindow(rows.FirstOrDefault()?.Head ?? 0, rows.ToArray());
    }

    /// <summary>Resolves the stream and reads at most one creation event or two matching command events.</summary>
    /// <param name="stream">The exact event stream name.</param>
    /// <param name="commandId">The originating command identity.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The database result for the requested operation.</returns>
    public async Task<IReadOnlyList<EventStreamReadModel>> FindAggregateEventsAsync(
        string stream, Guid? commandId, CancellationToken cancellationToken)
    {
        var sql = commandId.HasValue
            ? EventSourceDbSql.ReadAggregateEventByCommand
            : EventSourceDbSql.ReadAggregateCreation;
        var name = commandId.HasValue
            ? nameof(EventSourceDbSql.ReadAggregateEventByCommand)
            : nameof(EventSourceDbSql.ReadAggregateCreation);
        var preparedParameters34 = new AggregateFindParameters(stream, commandId);
        var rows = await _dbFactory.ActorEventSourceDb.Use($"{nameof(EventSourceDbSql)}.{name}", sql)
            .SetParameters(preparedParameters34)
            .ExecuteQueryAsync(MapToUnindexedAggregateEvent, cancellationToken)
            .ConfigureAwait(false);
        return rows.ToArray();
    }

    /// <summary>Atomically appends a sidecar snapshot and indexes its authoritative source revision.</summary>
    /// <param name="stream">The exact event stream name.</param>
    /// <param name="sidecar">The sidecar supplied by the caller.</param>
    /// <param name="sourceRevision">The source revision supplied by the caller.</param>
    /// <param name="snapshot">The snapshot supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>True when the requested condition holds.</returns>
    public Task<bool> SaveAggregateCheckpointAsync(string stream, string sidecar, long sourceRevision,
        IEvent snapshot, CancellationToken cancellationToken)
        => _aggregateCheckpointTransactions.Value.ExecuteAsync(
            (db, token) => db.AppendAggregateCheckpointAsync(stream, sidecar, sourceRevision, snapshot, token),
            cancellationToken);

    /// <summary>Appends an aggregate transition and sidecar checkpoint under one optimistic source fence.</summary>
    /// <param name="stream">The exact event stream name.</param>
    /// <param name="expectedRevision">The expected revision supplied by the caller.</param>
    /// <param name="source">The source supplied by the caller.</param>
    /// <param name="checkpoint">The checkpoint supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>True when the requested condition holds.</returns>
    public Task<bool> SaveAggregateSourceWithCheckpointAsync(string stream, long expectedRevision,
        IEvent source, IEvent checkpoint, CancellationToken cancellationToken)
        => _aggregateCheckpointTransactions.Value.ExecuteAsync(async (db, token) =>
        {
            await db.AppendAsync(new EnlistedEventAppend(stream, source.CommandId, source, expectedRevision), token)
            .ConfigureAwait(false);
            return await db.AppendAggregateCheckpointAsync(stream, $"{stream}.Snapshot", checked(expectedRevision + 1),
                checkpoint, token)
            .ConfigureAwait(false);
        }, cancellationToken);

    internal static AggregateCheckpointRow MapToAggregateCheckpointRow(IObjectDataRecord row)
        => new(row.GetLong(0), row.IsNull(1) ? AggregateCheckpointRowKind.None : (AggregateCheckpointRowKind)row.GetInt(1),
            row.IsNull(2) ? 0 : row.GetLong(2), row.IsNull(3) ? null : MapToAggregateEvent(row, 3));

    internal static ConcernCheckpointRow MapToConcernCheckpointRow(IObjectDataRecord row)
        => new(row.GetLong(0), row.GetLong(1), row.GetLong(2), row.GetBool(3), row.GetBool(4),
            row.IsNull(5) ? null : MapToAggregateEvent(row, 5));

    internal static EventStreamReadModel MapToUnindexedAggregateEvent(IObjectDataRecord row) => MapToAggregateEvent(row, 0);

    static EventStreamReadModel MapToAggregateEvent(IObjectDataRecord row, int offset)
        => new()
        {
            EventVersion = row.GetLong(offset),
            EventTypeName = row.GetString(offset + 1),
            StreamVersion = row.GetLong(offset + 2),
            EventData = row.GetBytes(offset + 3),
            Origin = MapToOrigin(row)
        };


    /// <summary>Reads a concern and its legacy prefix at one PostgreSQL statement boundary.</summary>
    /// <param name="concernStream">The concern stream supplied by the caller.</param>
    /// <param name="legacyStream">The legacy stream supplied by the caller.</param>
    /// <param name="checkpointType">The checkpoint type supplied by the caller.</param>
    /// <param name="useCheckpoint">The use checkpoint supplied by the caller.</param>
    /// <param name="reduce">The reduce supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    public async Task ReadConcernCheckpointWindowAsync(string concernStream, string legacyStream,
        Type checkpointType, bool useCheckpoint, Action<IEnumerable<ConcernCheckpointRow>> reduce,
        CancellationToken cancellationToken)
    {
        var identities = EventTypeRegistry.Default.GetReadIdentities(checkpointType)
            .Distinct(StringComparer.Ordinal).ToArray();
        var preparedParameters35 = new ConcernWindowParameters(concernStream, legacyStream, identities, useCheckpoint);
        await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.ReadConcernCheckpointWindow)}", EventSourceDbSql.ReadConcernCheckpointWindow)
            .SetParameters(preparedParameters35)
            .ExecuteMapReduceAsync(MapToConcernCheckpointRow, reduce, cancellationToken)
            .ConfigureAwait(false);
    }



    /// <summary>Maps the selected database columns in their query ordinal order.</summary>
    private static bool MapToHasEarlierUnresolvedEventProjectorExecutionRow1(global::TomasAI.IFM.Shared.Storage.IObjectDataRecord value) => value.GetBool(0);

    /// <summary>Maps the selected database columns in their query ordinal order.</summary>
    private static bool MapToHasEventForCommandRow2(global::TomasAI.IFM.Shared.Storage.IObjectDataRecord value) => value.GetBool(0);

    /// <summary>Maps the selected database columns in their query ordinal order.</summary>
    private static long MapToGetCommittedStreamVersionRow3(global::TomasAI.IFM.Shared.Storage.IObjectDataRecord row) => row.GetLong(0);
internal static long MapToReadCommittedStreamHeadRow1(global::TomasAI.IFM.Shared.Storage.IObjectDataRecord row) => row.GetLong(0);
internal static bool MapToInsertCommandLogCoreRow2(global::TomasAI.IFM.Shared.Storage.IObjectDataRecord value) => value.GetBool(0);
}
