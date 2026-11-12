namespace TomasAI.IFM.Application.Storage.EventSourceDb
{
    using Microsoft.Extensions.Logging;
    using System.Collections.Concurrent;
    using TomasAI.IFM.Framework.Storage;
    using TomasAI.IFM.Shared.Storage;
    using TomasAI.IFM.Shared.EventModelActor.Contracts;
    using TomasAI.IFM.Shared.EventProjector.ReadModels;
    using TomasAI.IFM.Shared.Exceptions;
    using TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;
    using TomasAI.IFM.Application.Storage.EventSourceDb.EventLog;
    using TomasAI.IFM.Shared.EventModelActor;
    using TomasAI.IFM.Shared.EventSourcing;
    using TomasAI.IFM.Shared.EventSourcing.ViewModels;
    using TomasAI.IFM.Shared.EventProjector;
    using TomasAI.IFM.Framework.Storage.Postgres;
    using TomasAI.IFM.Application.Storage.EventSourceDb.Diagnostics;
    using Npgsql;
    using System.Diagnostics;
    using System.Data;
    using TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;
    using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
    using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;

    /// <summary>Internal persistence helpers and validation for the actor event-source context.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "C# 14 instance extensions retain their context receiver; static conversion changes the storage helper API.")]
    internal static class EventSourceActorDbContextExtensions
    {
        extension(EventSourceActorDbContext context)
        {

            /// <summary>Uses indexed stream metadata; no event deserialization or second identity read is required.</summary>
            internal async Task<long> ReadCommittedStreamHeadAsync(string eventStream, CancellationToken cancellationToken)
            {
            var preparedParameters1 = new GetEventStreamId(eventStream);
                return await context.Database.Use(nameof(EventSourceActorDbSql.GetResidentStreamHead), EventSourceActorDbSql.GetResidentStreamHead)
                .SetParameters(preparedParameters1)
                .ExecuteSingleAsync(EventSourceActorDbContext.MapToReadCommittedStreamHeadRow1, cancellationToken)
                .ConfigureAwait(false);
            }

            /// <summary>Resolves the cached event stream identifier, creating the database entry on a cache miss.</summary>
            /// <param name="eventStream">The event stream name.</param>
            /// <param name="cancellationToken">The token used to cancel the operation.</param>
            /// <returns>The typed result of the event-source operation.</returns>
            internal Task<long> ResolveEventStreamIdAsync(string eventStream, CancellationToken cancellationToken)
                => context._streamIdentities.GetAsync(eventStream, context._streamIdentityLoader, cancellationToken).AsTask();
            /// <summary>Event Log Sql using the event-source persistence services.</summary>
            /// <param name="sql">The sql used by this operation.</param>
            /// <returns>The typed result of the event-source operation.</returns>
            internal string EventLogSql(string sql) => context._eventLogSqlLayout.Resolve(sql);

            /// <summary>Create Event Log Appender using the event-source persistence services.</summary>
            /// <param name="connectionString">The connection string used by this operation.</param>
            /// <param name="options">The event log persistence configuration.</param>
            /// <returns>The typed result of the event-source operation.</returns>
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Appender construction belongs to the context extension API.")]
            internal IEventLogAppender CreateEventLogAppender(string connectionString, EventLogPersistenceOptions options)
            {
                options.Validate();
                return options.WriteMode switch
                {
                    EventLogWriteMode.Sequential => new SequentialEventLogAppender(connectionString, options.UseLz4Compression, options),
                    EventLogWriteMode.BinaryCopy => new BinaryCopyEventLogAppender(connectionString, options.UseLz4Compression, options),
                    _ => throw new ArgumentOutOfRangeException(nameof(options))
                };
            }

            /// <summary>Apply Assignments using the event-source persistence services.</summary>
            /// <param name="domainEvents">The ordered events to persist.</param>
            /// <param name="result">The result used by this operation.</param>
            /// <returns>The typed result of the event-source operation.</returns>
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep internal persistence helpers on the context extension API.")]
            internal DomainEventCollection ApplyAssignments(DomainEventCollection domainEvents, EventLogAppendResult result)
            {
                if (result.Assignments.Count != domainEvents.Count)
                    throw new InvalidOperationException("The event-log appender returned an invalid assignment count.");
                var saved = new DomainEventCollection();
                for (var index = 0; index < domainEvents.Count; index++)
                {
                    EventInitHelper.SetProperty(domainEvents[index], nameof(IEvent.EventId), result.Assignments[index].EventVersion);
                    saved.Add(domainEvents[index]);
                }
                return saved;
            }

            /// <summary>Acquire Atomic Preparation using the event-source persistence services.</summary>
            /// <param name="streamId">The event stream identifier.</param>
            /// <returns>The typed result of the event-source operation.</returns>
            internal AtomicPreparationLease AcquireAtomicPreparation(string streamId)
            {
                while (true)
                {
                    var lane = context._atomicPreparationLanes.GetOrAdd(streamId, static _ => new AtomicPreparationLane());
                    lock (lane.Sync)
                    {
                        if (lane.Retired) continue;
                        lane.Users++;
                        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                        var lease = new AtomicPreparationLease(lane, lane.Tail, release);
                        lane.Tail = release.Task;
                        return lease;
                    }
                }
            }

            /// <summary>Release Atomic Preparation using the event-source persistence services.</summary>
            /// <param name="streamId">The event stream identifier.</param>
            /// <param name="preparation">The preparation used by this operation.</param>
            internal void ReleaseAtomicPreparation(string streamId, AtomicPreparationLease preparation)
            {
                preparation.Release.TrySetResult();
                lock (preparation.Lane.Sync)
                {
                    preparation.Lane.Users--;
                    if (preparation.Lane.Users != 0) return;
                    preparation.Lane.Retired = true;
                    context._atomicPreparationLanes.TryRemove(
                        new KeyValuePair<string, AtomicPreparationLane>(streamId, preparation.Lane));
                }
            }


            /// <summary>Prepare And Save Command Events Atomically using the event-source persistence services.</summary>
            /// <param name="preparation">The preparation used by this operation.</param>
            /// <param name="domainEvents">The ordered events to persist.</param>
            /// <param name="expectedStreamVersion">The committed stream version required for optimistic concurrency.</param>
            /// <param name="commandAudit">The command audit used by this operation.</param>
            /// <param name="cancellationToken">The token used to cancel the operation.</param>
            /// <returns>The typed result of the event-source operation.</returns>
            internal async Task<DomainEventCollection> PrepareAndSaveCommandEventsAtomicallyAsync(
                AtomicPreparationLease preparation,
                DomainEventCollection domainEvents,
                long expectedStreamVersion,
                CommandAuditEnvelope commandAudit,
                CancellationToken cancellationToken)
            {
                Task<EventLogAppendResult> persistence;
                try
                {
                    await preparation.Predecessor.WaitAsync(cancellationToken)
                .ConfigureAwait(false);
                    var streamId = await context.GetEventStreamIdAsync(commandAudit.StreamId, cancellationToken)
                .ConfigureAwait(false);
                    var entries = new List<EventLogAppendEntry>(domainEvents.Count);
                    foreach (var domainEvent in domainEvents)
                    {
                        MessageOrigins.ForPublication(domainEvent, context._businessClock);
                        entries.Add(new EventLogAppendEntry(
                            await context.GetEventNameIdFromDomainEventAsync(domainEvent, cancellationToken)
                .ConfigureAwait(false),
                            domainEvent));
                    }
                    persistence = context._atomicCommandEventAppender.Value.AppendAsync(new EventLogAppendRequest(
                        commandAudit.StreamId,
                        streamId,
                        commandAudit.CommandId,
                        entries,
                        expectedStreamVersion,
                        context._businessClock.GetUtcNow().UtcDateTime,
                        commandAudit), cancellationToken).AsTask();
                }
                finally
                {
                    context.ReleaseAtomicPreparation(commandAudit.StreamId, preparation);
                }

                var result = await persistence.ConfigureAwait(false);
                return context.ApplyAssignments(domainEvents, result);
            }


            /// <summary>Insert Command Log Core using the event-source persistence services.</summary>
            /// <param name="command">The command being audited or committed.</param>
            /// <param name="commandTimestamp">The command audit timestamp.</param>
            /// <param name="commandData">The serialized command audit payload.</param>
            /// <param name="cancellationToken">The token used to cancel the operation.</param>
            /// <returns>The typed result of the event-source operation.</returns>
            internal Task<bool> InsertCommandLogCoreAsync(
                ICommand command,
                DateTime commandTimestamp,
                string commandData,
                CancellationToken cancellationToken)
            {
                var parameters = new InsertActorCommandLog(command.CommandId, command.StreamId, $"{command.RouteTo}",
                    command.CommandName, $"{commandTimestamp:o}", $"{CommandStatus.InProgress}", commandData);
                return context._dbFactory.ActorEventSourceDb
                    .Use($"{nameof(EventSourceActorDbSql)}.{nameof(EventSourceActorDbSql.TryInsertCommandLog)}", EventSourceActorDbSql.TryInsertCommandLog)
                    .SetParameters(parameters)
                    .ExecuteScalarAsync(EventSourceActorDbContext.MapToInsertCommandLogCoreRow2, cancellationToken);
            }

            /// <summary>
            /// Asynchronously retrieves the unique identifier for the specified event stream.
            /// </summary>
            /// <remarks>If the event stream does not exist, it will be created and its identifier will be
            /// returned.</remarks>
            /// <param name="eventStream">The name of the event stream to retrieve the identifier for. Cannot be null or empty.</param>
            /// <returns>A <see langword="long"/> representing the unique identifier of the specified event stream.</returns>
            internal async Task<long> GetEventStreamAsync(string eventStream)
            {
                return await context.ResolveEventStreamIdAsync(eventStream, CancellationToken.None)
                .ConfigureAwait(false);
            }

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
                => await (context._dbFactory.ActorEventSourceDb
                        .Use($"{nameof(EventSourceActorDbSql)}.{nameof(EventSourceActorDbSql.GetEventLogByEventStreamId)}", context.EventLogSql(EventSourceActorDbSql.GetEventLogByEventStreamId))
                .SetParameters(new GetEventLogByEventStreamId(eventStreamId))
                .ExecuteQueryAsync<EventStreamReadModel>(EventSourceActorDbContext.MapToEventStream))
                .ConfigureAwait(false);

            /// <summary>
            /// Inserts an event stream into the database if it does not already exist.
            /// </summary>
            /// <remarks>The atomic database upsert makes concurrent first use of the same stream safe.</remarks>
            /// <param name="eventStream">The name of the event stream to insert. This value cannot be null or empty.</param>
            /// <returns>The unique identifier of the event stream. If the event stream already exists, its existing identifier is
            /// returned; otherwise, the identifier of the newly inserted event stream is returned.</returns>
            internal async Task<long> InsertEventStreamAsync(string eventStream)
                => await context.InsertEventStreamAsync(eventStream, CancellationToken.None)
                .ConfigureAwait(false);

            /// <summary>Insert Event Stream using the event-source persistence services.</summary>
            /// <param name="eventStream">The event stream name.</param>
            /// <param name="cancellationToken">The token used to cancel the operation.</param>
            /// <returns>The typed result of the event-source operation.</returns>
            internal async Task<long> InsertEventStreamAsync(string eventStream, CancellationToken cancellationToken)
            {
            var preparedParameters2 = new GetEventStreamId(eventStream);
                var eventStreamIdModel = await context._dbFactory.ActorEventSourceDb
                    .Use($"{nameof(EventSourceActorDbSql)}.{nameof(EventSourceActorDbSql.GetEventStreamId)}", EventSourceActorDbSql.GetEventStreamId)
                .SetParameters(preparedParameters2)
                .ExecuteSingleAsync<EventStreamIdReadModel>(EventSourceActorDbContext.MapToEventStreamId, cancellationToken)
                .ConfigureAwait(false);
                if (eventStreamIdModel is not null)
                    return eventStreamIdModel.EventStreamId;
            var preparedParameters3 = new InsertEventStreamId(eventStream);

                return await (context._dbFactory.ActorEventSourceDb
                    .Use($"{nameof(EventSourceActorDbSql)}.{nameof(EventSourceActorDbSql.InsertEventStreamId)}", EventSourceActorDbSql.InsertEventStreamId)
                .SetParameters(preparedParameters3)
                .ExecuteScalarAsync(EventSourceActorDbContext.MapToLong, cancellationToken))
                .ConfigureAwait(false);
            }


            /// <summary>
            /// Asynchronously retrieves the unique identifier for the event name associated with the specified event type.
            /// </summary>
            /// <typeparam name="TEvent">The type of the event, which must implement the <see cref="IEvent"/> interface.</typeparam>
            /// <returns>A task that represents the asynchronous operation. The task result contains the unique identifier  for the event
            /// name associated with the specified event type.</returns>
            internal async Task<int> GetEventNameIdFromTypeAsync<TEvent>() where TEvent : IEvent
                => await context.GetEventNameIdFromTypeAsync<TEvent>(CancellationToken.None)
                .ConfigureAwait(false);

            /// <summary>Get Event Name Id From Type using the event-source persistence services.</summary>
            /// <param name="cancellationToken">The token used to cancel the operation.</param>
            /// <typeparam name="TEvent">The event, snapshot, state or record type used by the operation.</typeparam>
            /// <returns>The typed result of the event-source operation.</returns>
            internal async Task<int> GetEventNameIdFromTypeAsync<TEvent>(CancellationToken cancellationToken) where TEvent : IEvent
                => await context.GetEventNameIdFromTypeAsync(typeof(TEvent), cancellationToken)
                .ConfigureAwait(false);

            /// <summary>
            /// Asynchronously retrieves the unique identifier for an event name based on the specified event type.
            /// </summary>
            /// <remarks>This method interacts with a data source to retrieve or insert the event name identifier. If
            /// the event name  identifier does not exist, it will be created and stored in the database.</remarks>
            /// <param name="eventType">The <see cref="Type"/> of the event for which the identifier is to be retrieved.  The <see
            /// cref="Type.FullName"/> and <see cref="Type.Name"/> properties are used to determine the event name and type.</param>
            /// <returns>A task that represents the asynchronous operation. The task result contains the unique identifier  for the event
            /// name associated with the specified event type.</returns>
            /// <param name="cancellationToken">The token used to cancel the operation.</param>
            internal async Task<int> GetEventNameIdFromTypeAsync(Type eventType, CancellationToken cancellationToken)
            {
                var identity = EventWriteIdentityRegistry.Default.GetIdentity(eventType);
                var eventTypeFullName = identity.EventTypeName;
                if (!context._eventNameIdCache.TryGetValue(eventTypeFullName, out EventNameIdReadModel eventNameIdModel))
                {
                    eventNameIdModel = await context.GetEventNameIdFromDbAsync(identity.EventName, eventTypeFullName, cancellationToken)
                .ConfigureAwait(false);
                    if (eventNameIdModel.IsValid)
                    {
                        context._eventNameIdCache.TryAdd(eventTypeFullName, eventNameIdModel);
                        return eventNameIdModel.EventNameId;
                    }
                    var newEventNameIdModel = await InsertEventNameIdAsync(identity.EventName, eventTypeFullName)
                .ConfigureAwait(false);
                    context._eventNameIdCache.TryAdd(eventTypeFullName, newEventNameIdModel);
                    return newEventNameIdModel.EventNameId;
                }
                return eventNameIdModel.EventNameId;

                async Task<EventNameIdReadModel> InsertEventNameIdAsync(string eventName, string eventTypeName)
                {
                    var eventNameIdModel = await context.GetEventNameIdFromDbAsync(eventName, eventTypeName, cancellationToken)
                .ConfigureAwait(false);
                    if (eventNameIdModel.IsValid)
                        return eventNameIdModel;
            var preparedParameters4 = new InsertEventNameId(eventName, eventTypeName);
                    var eventNameId = await (context._dbFactory.ActorEventSourceDb
                          .Use($"{nameof(EventSourceActorDbSql)}.{nameof(EventSourceActorDbSql.InsertEventNameId)}", EventSourceActorDbSql.InsertEventNameId)
                .SetParameters(preparedParameters4)
                .ExecuteScalarAsync(EventSourceActorDbContext.MapToInt, cancellationToken))
                .ConfigureAwait(false);
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
                => await context.GetEventNameIdFromDbAsync(eventName, eventTypeName, CancellationToken.None)
                .ConfigureAwait(false);

            /// <summary>Get Event Name Id From Db using the event-source persistence services.</summary>
            /// <param name="eventName">The event name used by this operation.</param>
            /// <param name="eventTypeName">The event type name used by this operation.</param>
            /// <param name="cancellationToken">The token used to cancel the operation.</param>
            /// <returns>The typed result of the event-source operation.</returns>
            internal async Task<EventNameIdReadModel> GetEventNameIdFromDbAsync(
                string eventName,
                string eventTypeName,
                CancellationToken cancellationToken)
                => await (context._dbFactory.ActorEventSourceDb
                    .Use($"{nameof(EventSourceActorDbSql)}.{nameof(EventSourceActorDbSql.GetEventNameId)}", EventSourceActorDbSql.GetEventNameId)
                .SetParameters(new GetEventNameId(eventName, eventTypeName))
                .ExecuteSingleAsync<EventNameIdReadModel>(EventSourceActorDbContext.MapToEventNameId, cancellationToken))
                .ConfigureAwait(false);

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
            var preparedParameters5 = new GetEventLogLastNRange(eventStreamId);
                var eventLogRange = await (context._dbFactory.ActorEventSourceDb
                    .Use($"{nameof(EventSourceActorDbSql)}.{nameof(EventSourceActorDbSql.GetEventLogLastNRange)}", context.EventLogSql(EventSourceActorDbSql.GetEventLogLastNRange))
                .SetParameters(preparedParameters5)
                .ExecuteQueryAsync<EventStreamReadModel>(EventSourceActorDbContext.MapToEventStream))
                .ConfigureAwait(false);
                return (eventLogRange is null || eventLogRange.Count == 0)
                    ? [] : [.. eventLogRange.Take(lastNRange).OrderBy(e => e.EventVersion)];
            }

                /// <summary>Executes repository preparation under the owning context or caller transaction.</summary>
            internal async ValueTask ReadCreationSnapshotAsync(long eventStreamId,
                Action<IEnumerable<EventStreamReadModel>> reducerAction, int[]? checkpointNames,
                CancellationToken cancellationToken, bool stateless = false, bool components = false,
                string[]? checkpointIdentities = null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                long head = 0;
                long replayedVersion = 0;
                var sql = components ? EventSourceActorDbSql.GetEventLogFromLatestComponentSnapshots
                    : stateless ? EventSourceActorDbSql.GetStatelessEventLogFromCreationSnapshot
                    : checkpointIdentities is not null ? EventSourceActorDbSql.GetEventLogFromCreationWithCheckpointIdentities
                    : checkpointNames is null ? EventSourceActorDbSql.GetEventLogFromCreationSnapshot : EventSourceActorDbSql.GetEventLogFromCreationWithCheckpoint;
                var statementName = components ? nameof(EventSourceActorDbSql.GetEventLogFromLatestComponentSnapshots)
                    : stateless ? nameof(EventSourceActorDbSql.GetStatelessEventLogFromCreationSnapshot)
                    : checkpointIdentities is not null ? nameof(EventSourceActorDbSql.GetEventLogFromCreationWithCheckpointIdentities)
                    : checkpointNames is null ? nameof(EventSourceActorDbSql.GetEventLogFromCreationSnapshot)
                    : nameof(EventSourceActorDbSql.GetEventLogFromCreationWithCheckpoint);
                var query = context._dbFactory.ActorEventSourceDb.Use($"{nameof(EventSourceActorDbSql)}.{statementName}", context.EventLogSql(sql));
                if (checkpointIdentities is not null)
                    query.SetParameters(new GetCheckpointEventLogByIdentity(eventStreamId, checkpointIdentities));
                else if (checkpointNames is null)
                    query.SetParameters(new GetEventLogByEventStreamId(eventStreamId));
                else
                    query.SetParameters(new GetMaxEventVersion(eventStreamId, checkpointNames));
                var allowCheckpoint = stateless || checkpointNames is not null || checkpointIdentities is not null;
                var hasCheckpointColumn = components || allowCheckpoint;
                await query.ExecuteMapReduceAsync(row => EventSourceActorDbContext.MapToCreationReplayRow(row, hasCheckpointColumn), ReduceAndValidate, cancellationToken)
                .ConfigureAwait(false);
                if (replayedVersion != head)
                    throw new InvalidDataException($"Creation snapshot replay for stream {eventStreamId} ended at {replayedVersion}, expected {head}.");

                void ReduceAndValidate(IEnumerable<CreationReplayRow> rows)
                {
                    using var iterator = rows.GetEnumerator();
                    reducerAction(ReadRows());
                    // Validate the remaining rows even when a reducer stops reading early.
                    while (iterator.MoveNext())
                        Validate(iterator.Current);

                    IEnumerable<EventStreamReadModel> ReadRows()
                    {
                        while (iterator.MoveNext())
                        {
                            var row = iterator.Current;
                            Validate(row);
                            if (row.Event is { EventVersion: > 0 } source)
                                yield return source;
                        }
                    }
                }

                void Validate(CreationReplayRow row)
                {
                    head = row.Head;
                    if (row.Event is null)
                    {
                        if (head != 0)
                            throw new InvalidDataException($"Creation snapshot is missing for nonempty stream {eventStreamId}.");
                        return;
                    }
                    var version = row.Event.StreamVersion;
                    var isCheckpoint = components
                        ? replayedVersion >= 1 && version > replayedVersion && version == row.CheckpointVersion
                        : allowCheckpoint && replayedVersion == 1 && version == row.CheckpointVersion;
                    if (version != replayedVersion + 1 && !isCheckpoint)
                        throw new InvalidDataException($"Creation snapshot replay gap in stream {eventStreamId}: {replayedVersion} -> {version}.");
                    replayedVersion = version;
                }
            }
            /// <summary>Executes repository preparation under the owning context or caller transaction.</summary>
            internal async Task<int[]> GetCompatibleEventNameIdsAsync<TEvent>(CancellationToken cancellationToken) where TEvent : IEvent
            {
                var type = typeof(TEvent);
                var current = await context.GetEventNameIdFromTypeAsync(type, cancellationToken)
                .ConfigureAwait(false);
                var registry = EventTypeRegistry.Default;
            var preparedParameters6 = new GetCompatibleEventNameIds(type.Name, registry.GetReadIdentities(type));
                // Do not cache the alias row IDs: metadata may gain a new registered
                // spelling while the process is running. Match resolved type, not a short-name guess.
                var candidates = await context._dbFactory.ActorEventSourceDb
                    .Use(nameof(EventSourceActorDbSql.GetCompatibleEventNameIds), EventSourceActorDbSql.GetCompatibleEventNameIds)
                .SetParameters(preparedParameters6)
                .ExecuteQueryAsync<EventNameIdReadModel>(EventSourceActorDbContext.MapToEventNameId, cancellationToken)
                .ConfigureAwait(false);
                return candidates.Where(row => registry.Resolve(row.EventTypeName) == type)
                    .Select(row => row.EventNameId).Append(current).Distinct().ToArray();
            }
    }
        extension(IEnlistedPostgresTransaction transaction)
        {
            /// <summary>Reads explicitly selected source streams without choosing replay policy.</summary>
            internal Task<IReadOnlyList<EventStreamSourceReadModel>> ReadMaintenanceSourcesAsync(string[] exactStreams, string[] prefixes, CancellationToken token)
                => transaction.QueryAsync(EventSourceActorDbSql.ReadMaintenanceSourceEvents, (object? [])new MaintenanceSourceStreams(exactStreams, prefixes).Bind(), EventSourceActorDbContext.MapToMaintenanceSource, token);

            /// <summary>Checks for committed history matching the supplied prefixes and exclusions.</summary>
            internal async Task<bool> HasCommittedStreamWithPrefixAsync(string[] excludedStreams, string[] prefixes, CancellationToken token)
                => await transaction.ScalarAsync(EventSourceActorDbSql.HasCommittedStreamWithPrefix, (object? [])new MaintenanceSourceStreams(excludedStreams, prefixes).Bind(), token)
                .ConfigureAwait(false) is true;

            /// <summary>Reports whether a stream has committed source history without creating metadata.</summary>
            internal async Task<bool> HasCommittedMaintenanceStreamAsync(string stream, CancellationToken token)
                => await transaction.ScalarAsync(EventSourceActorDbSql.HasCommittedMaintenanceStream, (object? [])new MaintenanceSourceStream(stream).Bind(), token)
                .ConfigureAwait(false) is not null;
        }
    }

    internal readonly record struct CreationReplayRow(long Head, long CheckpointVersion, EventStreamReadModel? Event);

    internal sealed class AtomicPreparationLane
    {
        public object Sync { get; } = new();
        public Task Tail { get; set; } = Task.CompletedTask;
        public int Users { get; set; }
        public bool Retired { get; set; }
    }

    internal sealed record AtomicPreparationLease(
            AtomicPreparationLane Lane,
            Task Predecessor,
            TaskCompletionSource Release);

    /// <summary>SQL/event operations explicitly enlisted in a single request-owned transaction.</summary>
    /// <remarks>Only storage implementations use this surface; actors never execute SQL.</remarks>
    public sealed class EnlistedEventTransaction : IEnlistedPostgresTransaction
    {
        readonly NpgsqlConnection connection;
        readonly NpgsqlTransaction transaction;
        readonly EventLogMessagePackCodec eventLogCodec;
        readonly EventLogSqlLayout eventLogSqlLayout;
        readonly PostgresQueryTimeoutScope timeouts;
        internal (NpgsqlConnection Connection, NpgsqlTransaction Transaction) GetConnectionAndTransaction()
        {
            RequireActive();
            return (connection, transaction);
        }

        int completed;
        readonly TimeProvider businessClock;
        /// <summary>Business time shared with the owner; database execution deadlines remain physical.</summary>
        public TimeProvider BusinessTime => businessClock;
        internal EnlistedEventTransaction(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            EventLogMessagePackCodec eventLogCodec,
            EventLogSqlLayout eventLogSqlLayout, PostgresQueryTimeout? defaultTimeout = null, TimeProvider? timeProvider = null)
        {
            (this.connection, this.transaction, this.eventLogCodec, this.eventLogSqlLayout) =
                (connection, transaction, eventLogCodec, eventLogSqlLayout);
            timeouts = new(connection, transaction, defaultTimeout);
            businessClock = timeProvider ?? TimeProvider.System;
        }

        /// <summary>Executes parameterized SQL on the enlisted connection.</summary>
        public async Task<int> ExecuteAsync(string sql, object?[] parameters, CancellationToken cancellationToken, PostgresQueryTimeout? timeout = null)
        {
            ArgumentNullException.ThrowIfNull(parameters);
            RequireActive();
            await ConfigureTimeoutAsync(timeout, cancellationToken)
                .ConfigureAwait(false);
            var command = CreateCommand(sql, parameters);
            await using var commandDisposal = command.ConfigureAwait(false);
            return await command.ExecuteNonQueryAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>Sends ordered SQL statements in one round trip on the enlisted transaction.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2100", Justification = "Batch SQL comes from application storage contracts and values are bound separately.")]
        public async Task ExecuteBatchAsync(IReadOnlyList<(string Sql, object?[] Parameters)> statements, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(statements);
            RequireActive();
            await ConfigureTimeoutAsync(null, cancellationToken)
                .ConfigureAwait(false);
            var batch = new NpgsqlBatch(connection, transaction) { Timeout = 0 };
            await using var disposal = batch.ConfigureAwait(false);
            foreach (var (sql, parameters) in statements)
            {
                var command = new NpgsqlBatchCommand(eventLogSqlLayout.Resolve(sql));
                PostgresValueBinding.AddParameters(command, parameters);
                batch.BatchCommands.Add(command);
            }
            await batch.ExecuteNonQueryAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>Reads a scalar on the same transaction; null remains explicit.</summary>
        public async Task<object?> ScalarAsync(string sql, object?[] parameters, CancellationToken cancellationToken, PostgresQueryTimeout? timeout = null)
        {
            ArgumentNullException.ThrowIfNull(parameters);
            RequireActive();
            await ConfigureTimeoutAsync(timeout, cancellationToken)
                .ConfigureAwait(false);
            var command = CreateCommand(sql, parameters);
            await using var commandDisposal = command.ConfigureAwait(false);
            var value = await command.ExecuteScalarAsync(cancellationToken)
                .ConfigureAwait(false);
            return value is DBNull ? null : value;
        }

        /// <summary>Materializes a bounded storage query before disposing its reader.</summary>
        public async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object?[] parameters,
            Func<System.Data.Common.DbDataReader, T> map, CancellationToken cancellationToken, PostgresQueryTimeout? timeout = null)
        {
            ArgumentNullException.ThrowIfNull(map);
            ArgumentNullException.ThrowIfNull(parameters);
            RequireActive();
            await ConfigureTimeoutAsync(timeout, cancellationToken)
                .ConfigureAwait(false);
            var command = CreateCommand(sql, parameters);
            await using var commandDisposal = command.ConfigureAwait(false);
            var reader = await command.ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);
            await using var readerDisposal = reader.ConfigureAwait(false);
            var values = new List<T>();
            while (await reader.ReadAsync(cancellationToken)
                .ConfigureAwait(false)) values.Add(map(reader));
            return values;
        }

        /// <summary>Reads the highest committed event version visible in this transaction.</summary>
        public async Task<long> ReadCommittedEventCutAsync(CancellationToken cancellationToken)
            => (long)(await ScalarAsync(EventSourceActorDbSql.ReadCommittedEventCut, [], cancellationToken)
                .ConfigureAwait(false))!;

        /// <summary>Reads the committed head revision for the requested stream in this transaction.</summary>
        public async Task<long> ReadCommittedStreamRevisionAsync(string stream, CancellationToken cancellationToken)
            => (long)(await ScalarAsync(EventSourceActorDbSql.ReadCommittedStreamRevision, [stream], cancellationToken)
                .ConfigureAwait(false))!;

        /// <summary>Reads the latest committed event and its original payload for the requested stream.</summary>
        public async Task<EventStreamReadModel?> ReadLatestCommittedEventAsync(string stream, CancellationToken cancellationToken)
            => (await QueryAsync(EventSourceActorDbSql.ReadLatestCommittedEvent, [stream], MapToLatestCommittedEvent, cancellationToken)
                .ConfigureAwait(false)).SingleOrDefault();

        private static EventStreamReadModel MapToLatestCommittedEvent(System.Data.Common.DbDataReader row)
            => new() { EventTypeName = row.GetString(0), EventVersion = row.GetInt64(1), EventData = row.GetFieldValue<byte[]>(2) };

        /// <summary>Commits a full sidecar checkpoint at a known source revision without changing the source stream.</summary>
        public async Task<bool> AppendAggregateCheckpointAsync(string stream, string sidecar, long sourceRevision,
            IEvent snapshot, CancellationToken cancellationToken)
        {
            var head = await ScalarAsync(EventSourceActorDbSql.LockAggregateCheckpointSource, [stream], cancellationToken)
                .ConfigureAwait(false);
            if (head is not long committed || sourceRevision <= 0 || sourceRevision > committed)
                throw new InvalidOperationException("A checkpoint must identify a committed source revision.");
            if (await ScalarAsync(EventSourceActorDbSql.ReadAggregateCheckpointVersion, [stream, sourceRevision], cancellationToken)
                .ConfigureAwait(false) is not null)
                return false;
            var current = await ScalarAsync(EventSourceActorDbSql.ReadAggregateCheckpointHead, [sidecar], cancellationToken)
                .ConfigureAwait(false);
            var checkpoint = await AppendAsync(new EnlistedEventAppend(sidecar, snapshot.CommandId, snapshot, current is long version ? version : 0),
                cancellationToken)
                .ConfigureAwait(false);
            await ExecuteAsync(EventSourceActorDbSql.RegisterAggregateCheckpoint, [stream, sourceRevision, checkpoint], cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        /// <summary>Reserves a prepared audit envelope under the transaction and returns its technical receipt.</summary>
        public async Task<CommandAudit.CommandAuditReservationOutcome> ReserveCommandAuditAsync(
            CommandAudit.CommandAuditEnvelope envelope, CancellationToken cancellationToken = default)
        {
            RequireActive();
            await ConfigureTimeoutAsync(null, cancellationToken).ConfigureAwait(false);
            var results = await CommandAudit.CommandAuditPostgres.ReserveAsync(connection, transaction, [envelope], cancellationToken)
                .ConfigureAwait(false);
            var result = results[0];
            return result.PayloadConflict ? CommandAudit.CommandAuditReservationOutcome.PayloadConflict
                : result.Accepted ? CommandAudit.CommandAuditReservationOutcome.Accepted
                : CommandAudit.CommandAuditReservationOutcome.Existing;
        }

        /// <summary>Locks the existing command audit row and checks whether any event committed for that command.</summary>
        public async Task<bool> HasCommittedAuditEventsUnderLockAsync(Guid commandId, CancellationToken cancellationToken)
        {
            _ = await ScalarAsync(EventSourceActorDbSql.EnlistedEventTransaction_ReserveCommandAsync_1, [commandId], cancellationToken)
                .ConfigureAwait(false);
            return await ScalarAsync(EventSourceActorDbSql.EnlistedEventTransaction_ReserveCommandAsync_2, [commandId], cancellationToken)
                .ConfigureAwait(false) is true;
        }

        /// <summary>Checks for an event from this command on the requested stream.</summary>
        public async Task<bool> HasCommittedCommandEventsAsync(Guid commandId, string streamId, CancellationToken cancellationToken)
            => await ScalarAsync(EventSourceActorDbSql.EnlistedEventTransaction_ReserveCommandAsync_3, [commandId, streamId], cancellationToken)
                .ConfigureAwait(false) is true;

        /// <summary>Appends to the event stream using its optimistic version and standard serialization.</summary>
        public async Task<long> AppendAsync(EnlistedEventAppend request, CancellationToken cancellationToken)
        {
            RequireActive();
            using var trace = FinancialTelemetry.ActivitySource.StartActivity("financial.event.append");
            var (stream, commandId, domainEvent, expectedStreamVersion) =
                (request.Stream, request.CommandId, request.DomainEvent, request.ExpectedStreamVersion);
            var streamId = Convert.ToInt64(await ScalarAsync(EventSourceActorDbSql.EnlistedEventTransaction_AppendAsync_1, [stream], cancellationToken)
                .ConfigureAwait(false)
                ?? await ScalarAsync(EventSourceActorDbSql.InsertEventStreamId, [stream], cancellationToken)
                .ConfigureAwait(false),System.Globalization.CultureInfo.InvariantCulture);
            var type = domainEvent.GetType();
            var identity = EventWriteIdentityRegistry.Default.GetIdentity(type);
            // The existing upsert takes an UPDATE lock even when the registry row is unchanged.
            // Reading pre-registered metadata avoids serializing unrelated Portfolio transactions on an event name.
            var nameId = (int)(await ScalarAsync(EventSourceActorDbSql.EnlistedEventTransaction_AppendAsync_2, [identity.EventName, identity.EventTypeName], cancellationToken)
                .ConfigureAwait(false)
                ?? await ScalarAsync(EventSourceActorDbSql.InsertEventNameId, [identity.EventName, identity.EventTypeName], cancellationToken)
                .ConfigureAwait(false))!;
            using var serializeTrace = FinancialTelemetry.ActivitySource.StartActivity("financial.event.serialize");
            MessageOrigins.ForPublication(domainEvent, businessClock);
            var payloadBytes = eventLogCodec.Serialize(domainEvent);
            serializeTrace?.SetTag("event.payload.bytes", payloadBytes.Length);
            serializeTrace?.Stop();
            using var insertTrace = FinancialTelemetry.ActivitySource.StartActivity("financial.event.insert");
            var parameters = new InsertEventLogExpectedVersion(streamId, nameId, payloadBytes, commandId,
                $"{businessClock.GetUtcNow().UtcDateTime:o}", expectedStreamVersion, MessageOriginJson.Serialize(MessageOrigins.Get(domainEvent)));
            var eventId = await ScalarAsync(EventSourceActorDbSql.InsertEventLogExpectedVersion, (object?[])parameters.Bind(), cancellationToken)
                .ConfigureAwait(false);
            insertTrace?.Stop();
            if (eventId is not long id) throw new ConcurrencyException($"Event stream {stream} is not at expected version {expectedStreamVersion}.");
            // EventId is transport/storage metadata. The immutable business event identity remains domainEvent.Id.
            EventInitHelper.SetProperty(domainEvent, nameof(IEvent.EventId), id);
            if (domainEvent is IRequireDurableProjection { RequiresDurableProjection: true } required)
            {
                var rule = required.RequiredProjection;
                if (rule.InitialStage is not (TomasAI.IFM.Shared.EventProjector.EventProjectorStageType.PublishProcessingEvent
                    or TomasAI.IFM.Shared.EventProjector.EventProjectorStageType.ApplyProjection))
                    throw new ArgumentException("Invalid durable projection stage.", nameof(domainEvent));
                var now = DateTime.UtcNow;
                var marker = await ScalarAsync(EventSourceActorDbSql.TryCreateEventProjectorExecutionState, [id, rule.ActorName, rule.ProjectorName, false, 0, "Processing", rule.InitialStage.ToString(), string.Empty, $"{now:o}", $"{now:o}", now], cancellationToken)
                .ConfigureAwait(false);
                if (marker is null) throw new InvalidOperationException("Required durable projection marker was not persisted.");
            }
            return id;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2100", Justification = "This storage executor accepts application SQL from reviewed schema/query contracts; runtime values are sent as bound parameters rather than interpolated into SQL.")]
        NpgsqlCommand CreateCommand(string sql, object?[] parameters)
        {
            var command = new NpgsqlCommand(eventLogSqlLayout.Resolve(sql), connection, transaction) { CommandTimeout = 0 };
            foreach (var parameter in parameters)
                PostgresValueBinding.AddParameter(command, parameter);
            return command;
        }

        internal Task ConfigureTimeoutAsync(PostgresQueryTimeout? timeout, CancellationToken token)
        {
            RequireActive();
            return timeouts.ApplyAsync(timeout, token);
        }

        internal void Complete() => Interlocked.Exchange(ref completed, 1);

        void RequireActive()
        {
            if (Volatile.Read(ref completed) != 0)
                throw new ObjectDisposedException(nameof(EnlistedEventTransaction), "The enlisted transaction scope has completed.");
        }
    }

    /// <summary>Shared financial Command/Function unit of work. No transaction is stored in a singleton context.</summary>
    public sealed class PostgresEventTransaction(
        IDbConnectionSettings settings,
        EventLogPersistenceOptions? eventLogPersistenceOptions = null,
        PostgresQueryTimeout? defaultQueryTimeout = null, TimeProvider? timeProvider = null) : IPostgresEventTransaction
    {
        readonly PostgresQueryTimeout queryTimeout = (defaultQueryTimeout ?? PostgresQueryTimeout.Default).Validate();
        readonly string connectionString = ResolveConnectionString(settings, defaultQueryTimeout);
        readonly EventLogMessagePackCodec eventLogCodec = new(
            (eventLogPersistenceOptions ?? new EventLogPersistenceOptions()).Validate().UseLz4Compression);
        readonly EventLogSqlLayout eventLogSqlLayout = EventLogSqlLayout.ForProduction(
            (eventLogPersistenceOptions ?? new EventLogPersistenceOptions()).Validate());



        public static void ValidatePortfolioDatabaseIdentity(IDbConnectionSettings settings) =>
            PostgresDatabaseIdentity.RequireSamePhysicalDatabase(settings,
                EventSourceActorDbContext.EventSourceActorDbConnection,
                PortfolioDb.PortfolioDbContext.PortfolioDbConnection);

        static string ResolveConnectionString(IDbConnectionSettings settings, PostgresQueryTimeout? timeout)
        {
            ArgumentNullException.ThrowIfNull(settings);
            if (settings[PortfolioDb.PortfolioDbContext.PortfolioDbConnection] is not null)
                ValidatePortfolioDatabaseIdentity(settings);
            return new NpgsqlConnectionStringBuilder(settings[EventSourceActorDbContext.EventSourceActorDbConnection].ConnectionString)
            { CommandTimeout = (timeout ?? PostgresQueryTimeout.Default).CommandTimeoutSeconds }.ConnectionString;
        }

        /// <inheritdoc />
        public Task<T> ExecuteOnceAsync<T>(Func<EnlistedEventTransaction, CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(operation);
            return ExecuteAttemptAsync(operation, cancellationToken);
        }

        /// <inheritdoc />
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S1994", Justification = "The retry/date loop is terminated by explicit success, cancellation, or bounded-history exits in its body.")]
        public async Task<T> ExecuteAsync<T>(Func<EnlistedEventTransaction, CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(operation);
            for (var attempt = 0; ; attempt++)
            {
                using var trace = FinancialTelemetry.ActivitySource.StartActivity("financial.transaction");
                trace?.SetTag("financial.transaction.attempt", attempt + 1);
                var started = Stopwatch.GetTimestamp();
                try
                {
                    var result = await ExecuteAttemptAsync(operation, cancellationToken)
                .ConfigureAwait(false);
                    trace?.SetTag("financial.transaction.outcome", "committed");
                    FinancialTelemetry.Transaction(Stopwatch.GetElapsedTime(started).TotalMilliseconds, "committed"); return result;
                }
                catch (PostgresException error) when (attempt < 2 && error.SqlState is PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure)
                {
                    // Only a confirmed server rollback may replay this database-only delegate.
                    // COMMIT uncertainty, uniqueness conflicts, lock timeouts and application refusals never retry here.
                    FinancialTelemetry.Transaction(Stopwatch.GetElapsedTime(started).TotalMilliseconds, "rolled_back_retry");
                    trace?.SetTag("financial.transaction.outcome", "rolled_back_retry");
                    FinancialTelemetry.Retry();
                    await Task.Delay(TimeSpan.FromMilliseconds(10 * (attempt + 1)), cancellationToken)
                .ConfigureAwait(false);
                }
                catch (Exception error)
                {
                    var outcome = error switch
                    {
                        FunctionCommitOutcomeUnknownException => "unknown",
                        OperationCanceledException => "cancelled",
                        PostgresException { SqlState: PostgresErrorCodes.LockNotAvailable } => "lock_timeout",
                        PostgresException { SqlState: PostgresErrorCodes.QueryCanceled } => "statement_timeout",
                        _ => "failed"
                    };
                    trace?.SetTag("financial.transaction.outcome", outcome);
                    FinancialTelemetry.Transaction(Stopwatch.GetElapsedTime(started).TotalMilliseconds, outcome); throw;
                }
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Retain the existing bounded persistence failure boundary: complete cleanup or report failure while preserving ambiguous commit/recovery evidence and the original cause.")]
        async Task<T> ExecuteAttemptAsync<T>(Func<EnlistedEventTransaction, CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
        {
            var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(connectionString);
            await using var connectionDisposal = connection.ConfigureAwait(false);
            using (FinancialTelemetry.ActivitySource.StartActivity("financial.transaction.open_connection"))
                await connection.OpenAsync(cancellationToken)
                .ConfigureAwait(false);
            using var beginTrace = FinancialTelemetry.ActivitySource.StartActivity("financial.transaction.begin");
            var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
                .ConfigureAwait(false);
            await using var transactionDisposal = transaction.ConfigureAwait(false);
            beginTrace?.Stop();
            var enlisted = new EnlistedEventTransaction(connection, transaction, eventLogCodec, eventLogSqlLayout, queryTimeout, timeProvider);
            T result;
            try
            {
                // Limit lock/provider waits; caller cancellation can impose a stricter operation deadline.
                using (FinancialTelemetry.ActivitySource.StartActivity("financial.transaction.configure"))
                    await enlisted.ConfigureTimeoutAsync(null, cancellationToken)
                .ConfigureAwait(false);
                using (FinancialTelemetry.ActivitySource.StartActivity("financial.transaction.apply"))
                    result = await operation(enlisted, cancellationToken)
                .ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                await enlisted.ConfigureTimeoutAsync(null, cancellationToken)
                .ConfigureAwait(false);
            }
            catch
            {
                // COMMIT has not been sent. Disposal closes a connection if rollback itself cannot be confirmed.
                try
                {
                    using var rollbackTrace = FinancialTelemetry.ActivitySource.StartActivity("financial.transaction.rollback");
                    await transaction.RollbackAsync(CancellationToken.None)
                .ConfigureAwait(false);
                }
                catch { /* Preserve the original database failure; cleanup is best effort. */ }
                throw;
            }
            finally
            {
                enlisted.Complete();
            }
            try
            {
                using var commitTrace = FinancialTelemetry.ActivitySource.StartActivity("financial.transaction.commit");
                await transaction.CommitAsync(cancellationToken)
                .ConfigureAwait(false);
            }
            catch (PostgresException)
            {
                // A server error response (for example deferred constraint failure) confirms COMMIT failed.
                throw;
            }
            catch (Exception exception)
            {
                throw new FunctionCommitOutcomeUnknownException(
                    "PostgreSQL COMMIT was not acknowledged. Reconcile the original operation receipt before retrying.", exception);
            }
            return result; // Cancellation after confirmed commit cannot turn success into rollback.
        }
    }

    /// <summary>Compatibility entry point returning provider-neutral JSON metadata.</summary>
    public static class PostgresValues
    {
        public static object Jsonb(string json) => TomasAI.IFM.Shared.Storage.BindValues.Json(json);
    }

    /// <summary>Applies query-owned server timers on a caller-owned transaction without leaking settings to the pool.</summary>
    internal sealed class PostgresQueryTimeoutScope(NpgsqlConnection connection, NpgsqlTransaction transaction,
        PostgresQueryTimeout? defaults = null)
    {
        readonly PostgresQueryTimeout defaultTimeout = (defaults ?? PostgresQueryTimeout.Default).Validate();
        PostgresQueryTimeout? activeTimeout;

        internal async Task ApplyAsync(PostgresQueryTimeout? timeout, CancellationToken token)
        {
            var selected = (timeout ?? defaultTimeout).Validate();
            if (selected == activeTimeout) return;
            // The query owns the server timer. A second provider timer must not expire earlier.
            await using var configure = new NpgsqlCommand(
                EventSourceActorDbSql.PostgresQueryTimeoutScope_ApplyAsync_1, connection, transaction)
                { CommandTimeout = 0 };
            PostgresValueBinding.AddParameter(configure, checked((int)Math.Ceiling(selected.StatementTimeout.TotalMilliseconds)).ToString(System.Globalization.CultureInfo.InvariantCulture));
            PostgresValueBinding.AddParameter(configure, checked((int)Math.Ceiling((selected.LockTimeout ?? selected.StatementTimeout).TotalMilliseconds)).ToString(System.Globalization.CultureInfo.InvariantCulture));
            await configure.ExecuteNonQueryAsync(token)
                .ConfigureAwait(false);
            activeTimeout = selected;
        }
    }

    /// <summary>Server-enforced limits for one SQL statement; caller cancellation remains authoritative.</summary>
    public sealed record PostgresQueryTimeout(TimeSpan StatementTimeout, TimeSpan? LockTimeout = null)
    {
        /// <summary>Default SQL and lock wait budget when a query does not specify its own limits.</summary>
        public static PostgresQueryTimeout Default { get; } = new(TimeSpan.FromMinutes(2));

        /// <summary>Provider fallback for commands outside the enlisted query executor.</summary>
        public int CommandTimeoutSeconds => checked((int)Math.Ceiling(StatementTimeout.TotalSeconds));

        internal PostgresQueryTimeout Validate()
        {
            var locks = LockTimeout ?? StatementTimeout;
            if (StatementTimeout <= TimeSpan.Zero || StatementTimeout.TotalMilliseconds > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(StatementTimeout), "SQL timeout must be positive and fit PostgreSQL milliseconds.");
            if (locks <= TimeSpan.Zero || locks.TotalMilliseconds > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(LockTimeout), "Lock timeout must be positive and fit PostgreSQL milliseconds.");
            return this;
        }
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
    public readonly record struct PostgresDatabaseIdentity(string Host, int Port, string Database, string Username)
    {
        public static PostgresDatabaseIdentity Parse(string connectionString)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
            var value = new NpgsqlConnectionStringBuilder(connectionString);
            var hosts = value.Host.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (hosts.Length != 1)
                throw new InvalidOperationException("Atomic PostgreSQL transactions require one explicit database host.");
            if (string.IsNullOrWhiteSpace(value.Database))
                throw new InvalidOperationException("Atomic PostgreSQL transactions require one explicit database name.");
            return new(hosts[0].ToUpperInvariant(), value.Port, value.Database,
                value.Username ?? string.Empty);
        }

        public static void RequireSamePhysicalDatabase(IDbConnectionSettings settings, string firstName, string secondName)
        {
            ArgumentNullException.ThrowIfNull(settings);
            var first = settings[firstName] ?? throw new InvalidOperationException($"Connection setting '{firstName}' is required.");
            var second = settings[secondName] ?? throw new InvalidOperationException($"Connection setting '{secondName}' is required.");
            if (!first.ProviderName.Equals("System.Data.Postgres", StringComparison.Ordinal)
                || !second.ProviderName.Equals("System.Data.Postgres", StringComparison.Ordinal))
                throw new InvalidOperationException("Atomic persistence requires PostgreSQL for both named contexts.");
            if (Parse(first.ConnectionString) != Parse(second.ConnectionString))
                throw new InvalidOperationException("The enlisted contexts must identify the same physical PostgreSQL database and security identity.");
        }
    }

    /// <summary>Receipt-based scan includes late commits; a global sequence cursor cannot safely provide that guarantee.</summary>
    public sealed class PostgresCommittedBusinessEventJournal : ICommittedBusinessEventJournal
    {

        readonly string connection;
        readonly string pendingSql;
        readonly string priorSql;
        readonly string handoffsSql;

        public PostgresCommittedBusinessEventJournal(
            IDbConnectionSettings settings,
            EventLogPersistenceOptions? eventLogPersistenceOptions = null)
        {
            ArgumentNullException.ThrowIfNull(settings);
            var options = (eventLogPersistenceOptions ?? new EventLogPersistenceOptions()).Validate();
            var layout = EventLogSqlLayout.ForProduction(options);
            connection = settings[EventSourceActorDbContext.EventSourceActorDbConnection].ConnectionString;
            pendingSql = layout.Resolve(EventSourceActorDbSql.ReadPendingBusinessEvents);
            priorSql = layout.Resolve(EventSourceActorDbSql.ReadPriorBusinessEvent);
            handoffsSql = layout.Resolve(EventSourceActorDbSql.ReadPendingBusinessHandoffs);
        }

        public Task<IReadOnlyList<EventLogReadModel>> ReadPendingAsync(IReadOnlyList<string> eventNames, CancellationToken cancellationToken)
            => ReadAsync(pendingSql, [eventNames.ToArray()], cancellationToken);

        public Task<IReadOnlyList<EventLogReadModel>> ReadPendingHandoffsAsync(CancellationToken cancellationToken)
            => ReadAsync(handoffsSql, [], cancellationToken);

        public async Task CompleteHandoffAsync(long eventId, CancellationToken cancellationToken)
        {
            var db = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(connection);
            await using var dbDisposal = db.ConfigureAwait(false);
            await db.OpenAsync(cancellationToken)
                .ConfigureAwait(false);
            var command = new NpgsqlCommand(EventSourceActorDbSql.PostgresCommittedBusinessEventJournal_CompleteHandoffAsync_1, db) { CommandTimeout = 10 };
            await using var commandDisposal = command.ConfigureAwait(false);
            PostgresValueBinding.AddParameter(command, eventId);
            if (await command.ExecuteNonQueryAsync(cancellationToken)
                .ConfigureAwait(false) != 1)
                throw new InvalidDataException("Committed handoff receipt is missing.");
        }

        public async Task<EventLogReadModel?> ReadPriorAsync(long streamId, long throughEventId, IReadOnlyList<string> eventNames, CancellationToken cancellationToken)
            => (await ReadAsync(priorSql, [streamId, throughEventId, eventNames.ToArray()], cancellationToken)
                .ConfigureAwait(false)).SingleOrDefault();

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2100", Justification = "This storage executor accepts application SQL from reviewed schema/query contracts; runtime values are sent as bound parameters rather than interpolated into SQL.")]
        async Task<IReadOnlyList<EventLogReadModel>> ReadAsync(string sql, object[] arguments, CancellationToken cancellationToken)
        {
            var db = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(connection);
            await using var dbDisposal = db.ConfigureAwait(false);
            await db.OpenAsync(cancellationToken)
                .ConfigureAwait(false);
            var command = new NpgsqlCommand(sql, db) { CommandTimeout = 10 };
            await using var commandDisposal = command.ConfigureAwait(false);
            foreach (var arg in arguments) PostgresValueBinding.AddParameter(command, arg);
            var reader = await command.ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);
            await using var readerDisposal = reader.ConfigureAwait(false);
            var result = new List<EventLogReadModel>();
            while (await reader.ReadAsync(cancellationToken)
                .ConfigureAwait(false))
            {
                var payload = await reader.GetFieldValueAsync<byte[]>(4, cancellationToken)
                    .ConfigureAwait(false);
                if (payload.Length > 16 * 1024 * 1024)
                    throw new InvalidDataException("Business source event exceeds its bound.");
                result.Add(EventSourceActorDbContext.MapToJournalEvent(reader, payload));
            }
            return result;
        }

        public async Task RejectAsync(long eventId, string reasonCode, string detail, CancellationToken cancellationToken)
        {
            if (eventId <= 0 || string.IsNullOrWhiteSpace(reasonCode) || reasonCode.Length > 128 || string.IsNullOrWhiteSpace(detail) || detail.Length > 4096)
                throw new ArgumentException("A bounded committed-event rejection is required.",nameof(eventId));
            var db = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(connection);
            await using var dbDisposal = db.ConfigureAwait(false);
            await db.OpenAsync(cancellationToken)
                .ConfigureAwait(false);
            var transaction = await db.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await using var transactionDisposal = transaction.ConfigureAwait(false);
            using (var issue = new NpgsqlCommand(EventSourceActorDbSql.PostgresCommittedBusinessEventJournal_RejectAsync_1, db, transaction) { CommandTimeout = 10 })
            {
                PostgresValueBinding.AddParameter(issue, eventId); PostgresValueBinding.AddParameter(issue, reasonCode); PostgresValueBinding.AddParameter(issue, detail);
                await issue.ExecuteNonQueryAsync(cancellationToken)
                .ConfigureAwait(false);
            }
            using (var receipt = new NpgsqlCommand(EventSourceActorDbSql.PostgresCommittedBusinessEventJournal_RejectAsync_2, db, transaction) { CommandTimeout = 10 })
            {
                PostgresValueBinding.AddParameter(receipt, eventId);
                await receipt.ExecuteNonQueryAsync(cancellationToken)
                .ConfigureAwait(false);
            }
            await transaction.CommitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        public async Task AcknowledgeAsync(long eventId, CancellationToken cancellationToken)
        {
            var db = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(connection);
            await using var dbDisposal = db.ConfigureAwait(false);
            await db.OpenAsync(cancellationToken)
                .ConfigureAwait(false);
            var command = new NpgsqlCommand(EventSourceActorDbSql.PostgresCommittedBusinessEventJournal_RejectAsync_2, db) { CommandTimeout = 10 };
            await using var commandDisposal = command.ConfigureAwait(false);
            PostgresValueBinding.AddParameter(command, eventId);
            await command.ExecuteNonQueryAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>Receipt-based committed history projection, including events whose transactions commit after a newer sequence.</summary>
    public sealed class RiskHistoryJournal(IPostgresEventTransaction transactions)
    {
        public const string HistoryProjection = "risk-history-v1";
        public const string FundOutcomeProjection = "risk-fund-outcomes-v1";


        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1068", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        public Task<long> LoadCursorAsync(CancellationToken token = default, string projectionName = HistoryProjection)
            => transactions.ExecuteAsync(async (db, ct) =>
            {
                await (db.ExecuteAsync(Schema.EventSourceActorSchemaSql.CreateRiskHistoryJournal, [], ct))
                .ConfigureAwait(false);
                var value = await (db.ScalarAsync(EventSourceActorDbSql.RiskHistoryJournal_LoadCursorAsync_1, [projectionName], ct))
                .ConfigureAwait(false);
                var cursor = value is null ? 0 : Convert.ToInt64(value,System.Globalization.CultureInfo.InvariantCulture);
                // Compatible migration: the old cursor proves these events were previously projected.
                await (db.ExecuteAsync(EventSourceActorDbSql.RiskHistoryJournal_LoadCursorAsync_2, [projectionName, cursor], ct))
                .ConfigureAwait(false);
                return cursor;
            }, token);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1068", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        public Task SaveCursorAsync(long after, CancellationToken token = default, string projectionName = HistoryProjection)
            => transactions.ExecuteAsync(async (db, ct) =>
            {
                await (db.ExecuteAsync(EventSourceActorDbSql.RiskHistoryJournal_SaveCursorAsync_1, [after, projectionName], ct))
                .ConfigureAwait(false);
                return true;
            }, token);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1068", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        public Task<IReadOnlyList<WorkflowStrategyStateUpdatedEvent>> PageAsync(long after, CancellationToken token = default,
            string projectionName = HistoryProjection)
            => transactions.ExecuteAsync(async (db, ct) =>
            {
                var rows = await (db.QueryAsync(EventSourceActorDbSql.RiskHistoryJournal_PageAsync_1, [projectionName], EventSourceActorDbContext.MapToJournalEvent, ct))
                .ConfigureAwait(false);
                return (IReadOnlyList<WorkflowStrategyStateUpdatedEvent>)rows.Select(x => (WorkflowStrategyStateUpdatedEvent)x.ToDomainEvent()).ToArray();
            }, token);

        public Task AcknowledgeAsync(string projectionName, long eventId, CancellationToken token = default)
            => transactions.ExecuteAsync(async (db, ct) =>
            {
                await AcknowledgeAsync(db, projectionName, eventId, ct).ConfigureAwait(false);
                return true;
            }, token);

        /// <summary>Records the receipt on the caller's transaction alongside the projection write.</summary>
        public Task<int> AcknowledgeAsync(IEnlistedPostgresTransaction transaction, string projectionName, long eventId, CancellationToken token)
            => transaction.ExecuteAsync(EventSourceActorDbSql.RiskHistoryJournal_AcknowledgeAsync_1, [projectionName, eventId], token);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3928", Justification = "The parameter is captured by the transaction callback from the enclosing public method.")]
        public Task QuarantineConflictAsync(long eventId, RiskHistoryProjectionResult result, CancellationToken token = default)
            => transactions.ExecuteAsync(async (db, ct) =>
            {
                if (result.Disposition != RiskHistoryProjectionDisposition.Conflict || result.WorkflowId == Guid.Empty || result.InvocationId == Guid.Empty)
                    throw new ArgumentException("A complete Risk history conflict result is required.", nameof(result));
                await (db.ExecuteAsync(EventSourceActorDbSql.RiskHistoryJournal_QuarantineConflictAsync_1, [HistoryProjection, eventId, result.WorkflowId, result.InvocationId, result.Revision, result.StoredHash!, result.IncomingHash!], ct))
                .ConfigureAwait(false);
                await (db.ExecuteAsync(
                    EventSourceActorDbSql.RiskHistoryJournal_AcknowledgeAsync_1,
                    [HistoryProjection, eventId], ct))
                .ConfigureAwait(false);
                return true;
            }, token);

        /// <summary>Reads one ordered page of unacknowledged source events of the supplied name.</summary>
        public Task<IReadOnlyList<EventLogReadModel>> ReadPendingPageAsync(IEnlistedPostgresTransaction transaction,
            string eventName, string projectionName, CancellationToken token)
            => transaction.QueryAsync(EventSourceActorDbSql.ReadPendingRiskJournalEventsByName, [eventName, projectionName], EventSourceActorDbContext.MapToJournalEvent, token);
    }
}

namespace TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit
{
    using System.Data;
    using System.Threading.Channels;
    using Npgsql;
    using TomasAI.IFM.Framework.Storage.Postgres;
    using System.Security.Cryptography;
    using TomasAI.IFM.Shared.Storage;
    using System.Buffers;
    using MessagePack;
    using MessagePack.Resolvers;
    using TomasAI.IFM.Shared.EventSourcing;
    using TomasAI.IFM.Shared.EventModelActor;

    /// <summary>Single-consumer, event-driven PostgreSQL command-audit writer.</summary>
    internal sealed class PostgresCommandAuditWriter : ICommandAuditWriter
    {
        readonly string _connectionString;
        readonly CommandAuditPersistenceOptions _options;
        readonly Channel<PendingAudit> _queue;
        readonly CancellationTokenSource _lifetime = new();
        readonly Task _consumer;
        NpgsqlConnection? _connection;
        int _disposed;

        public PostgresCommandAuditWriter(string connectionString, CommandAuditPersistenceOptions options)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
            _connectionString = connectionString;
            _options = (options ?? throw new ArgumentNullException(nameof(options))).Validate();
            _queue = Channel.CreateBounded<PendingAudit>(new BoundedChannelOptions(_options.QueueCommandCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
            _consumer = ConsumeAsync(_lifetime.Token);
        }

        public async ValueTask<CommandAuditWriteResult> ReserveAsync(
            CommandAuditEnvelope envelope,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            if (envelope.PayloadBytes <= 0 || envelope.PayloadBytes > _options.MaximumCommandPayloadBytes)
                throw new ArgumentException("Command audit payload is outside its configured size limit.", nameof(envelope));

            var pending = new PendingAudit(envelope);
            await _queue.Writer.WriteAsync(pending, cancellationToken)
                .ConfigureAwait(false);
            return await pending.Completion.Task.ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Retain the existing bounded persistence failure boundary: complete cleanup or report failure while preserving ambiguous commit/recovery evidence and the original cause.")]
        async Task ConsumeAsync(CancellationToken cancellationToken)
        {
            PendingAudit? carry = null;
            try
            {
                while (carry is not null || await _queue.Reader.WaitToReadAsync(cancellationToken)
                .ConfigureAwait(false))
                {
                    var first = carry ?? (_queue.Reader.TryRead(out var queued) ? queued : null);
                    carry = null;
                    if (first is null) continue;

                    var batch = new List<PendingAudit> { first };
                    var bytes = first.Envelope.PayloadBytes;
                    var maximumCommands = _options.WriteMode == CommandAuditWriteMode.WindowedMessagePack
                        ? _options.MaximumCommandsPerBatch
                        : 1;
                    if (maximumCommands > 1)
                    {
                        using var deadlineCancellation = new CancellationTokenSource();
                        var deadline = Task.Delay(_options.MaximumOldestRequestDelay, deadlineCancellation.Token);
                        while (batch.Count < maximumCommands && bytes < _options.MaximumBatchBytes)
                        {
                            if (_queue.Reader.TryRead(out var next))
                            {
                                if (bytes + next.Envelope.PayloadBytes > _options.MaximumBatchBytes)
                                {
                                    carry = next;
                                    break;
                                }
                                batch.Add(next);
                                bytes += next.Envelope.PayloadBytes;
                                continue;
                            }

                            var available = _queue.Reader.WaitToReadAsync(cancellationToken).AsTask();
                            var completed = await Task.WhenAny(available, deadline)
                .ConfigureAwait(false);
                            if (completed == deadline) break;
                            if (!await available.ConfigureAwait(false)) break;
                        }
    await                    deadlineCancellation.CancelAsync()
                .ConfigureAwait(false);
                    }

                    await PersistAsync(batch, cancellationToken)
                .ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                var failure = new OperationCanceledException("Command audit writer stopped before persistence completed.");
                if (carry is not null) carry.Completion.TrySetException(failure);
                while (_queue.Reader.TryRead(out var pending)) pending.Completion.TrySetException(failure);
            }
            catch (Exception exception)
            {
                if (carry is not null) carry.Completion.TrySetException(exception);
                while (_queue.Reader.TryRead(out var pending)) pending.Completion.TrySetException(exception);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Retain the existing bounded persistence failure boundary: complete cleanup or report failure while preserving ambiguous commit/recovery evidence and the original cause.")]
        async Task PersistAsync(IReadOnlyList<PendingAudit> batch, CancellationToken cancellationToken)
        {
            try
            {
                var connection = await GetOpenConnectionAsync(cancellationToken)
                .ConfigureAwait(false);
                var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
                .ConfigureAwait(false);
            await using var transactionDisposal = transaction.ConfigureAwait(false);
                try
                {
                    var results = await CommandAuditPostgres.ReserveAsync(
                        connection, transaction, batch.Select(static item => item.Envelope).ToArray(), cancellationToken)
                .ConfigureAwait(false);
                    await transaction.CommitAsync(cancellationToken)
                .ConfigureAwait(false);
                    for (var index = 0; index < batch.Count; index++)
                    {
                        if (results[index].PayloadConflict)
                        {
                            batch[index].Completion.TrySetException(
                                new CommandAuditPayloadConflictException(batch[index].Envelope.CommandId));
                            continue;
                        }

                        batch[index].Completion.TrySetResult(results[index]);
                    }
                }
                catch
                {
                    try { await transaction.RollbackAsync(CancellationToken.None)
                .ConfigureAwait(false); } catch { /* Preserve the original database failure; cleanup is best effort. */ }
                    throw;
                }
            }
            catch (Exception exception)
            {
                if (_connection is not { FullState: ConnectionState.Open })
                {
                    if (_connection is not null) await _connection.DisposeAsync()
                .ConfigureAwait(false);
                    _connection = null;
                }
                foreach (var pending in batch) pending.Completion.TrySetException(exception);
            }
        }

        async Task<NpgsqlConnection> GetOpenConnectionAsync(CancellationToken cancellationToken)
        {
            if (_connection is { FullState: ConnectionState.Open }) return _connection;
            if (_connection is not null) await _connection.DisposeAsync()
                .ConfigureAwait(false);
            _connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(_connectionString);
            await _connection.OpenAsync(cancellationToken)
                .ConfigureAwait(false);
            return _connection;
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _queue.Writer.TryComplete();
            var completed = await Task.WhenAny(_consumer, Task.Delay(_options.ShutdownDrainTimeout))
                .ConfigureAwait(false);
            if (completed != _consumer)
            {
    await            _lifetime.CancelAsync()
                .ConfigureAwait(false);
                await _consumer.ConfigureAwait(false);
            }
            else
            {
                await _consumer.ConfigureAwait(false);
            }
            if (_connection is not null) await _connection.DisposeAsync()
                .ConfigureAwait(false);
            _lifetime.Dispose();
        }

        sealed class PendingAudit(CommandAuditEnvelope envelope)
        {
            public CommandAuditEnvelope Envelope { get; } = envelope;
            public TaskCompletionSource<CommandAuditWriteResult> Completion { get; } =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    internal static class CommandAuditPostgres
    {
        internal static async Task<CommandAuditWriteResult[]> ReserveAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            IReadOnlyList<CommandAuditEnvelope> envelopes,
            CancellationToken cancellationToken)
        {
            var command = new NpgsqlCommand(
                EventSourceActorDbSql.InsertCommandLogMessagePackWindow, connection, transaction)
            { CommandTimeout = PostgresQueryTimeout.Default.CommandTimeoutSeconds };
            await using var commandDisposal = command.ConfigureAwait(false);
            Add(command, envelopes.Select(static item => item.CommandId).ToArray());
            Add(command, envelopes.Select(static item => item.StreamId).ToArray());
            Add(command, envelopes.Select(static item => item.ActorName).ToArray());
            Add(command, envelopes.Select(static item => item.CommandName).ToArray());
            Add(command, envelopes.Select(static item => $"{item.CommandTimestampUtc:o}").ToArray());
            Add(command, Enumerable.Repeat(CommandStatus.InProgress.ToString(), envelopes.Count).ToArray());
            Add(command, Enumerable.Repeat(string.Empty, envelopes.Count).ToArray());
            Add(command, envelopes.Select(static item => item.Payload.Bytes).ToArray());
            Add(command, envelopes.Select(static item => (short)item.Payload.Format).ToArray());
            Add(command, envelopes.Select(static item => item.Payload.Version).ToArray());
            Add(command, envelopes.Select(static item => item.Payload.Sha256).ToArray());
            Add(command, envelopes.Select(static item => TomasAI.IFM.Shared.EventModelActor.MessageOriginJson.Serialize(item.Origin)).ToArray());

            var results = new CommandAuditWriteResult[envelopes.Count];
            var reader = await command.ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);
            await using var readerDisposal = reader.ConfigureAwait(false);
            var count = 0;
            while (await reader.ReadAsync(cancellationToken)
                .ConfigureAwait(false))
            {
                var ordinal = checked((int)reader.GetInt64(0) - 1);
                if ((uint)ordinal >= (uint)results.Length) throw new InvalidDataException("Invalid command audit ordinal.");
                var accepted = reader.GetBoolean(2);
                var payloadConflict = false;
                if (!accepted && !(await reader.IsDBNullAsync(3,cancellationToken)
                .ConfigureAwait(false)))
                {
                    var storedHash = (await reader.GetFieldValueAsync<byte[]>(3,cancellationToken)
                .ConfigureAwait(false));
                    if (storedHash.Length != SHA256.HashSizeInBytes ||
                        !CryptographicOperations.FixedTimeEquals(storedHash, envelopes[ordinal].Payload.Sha256))
                        payloadConflict = true;
                }
                results[ordinal] = new CommandAuditWriteResult(
                    accepted,
                    !accepted && (await reader.IsDBNullAsync(3,cancellationToken)
                .ConfigureAwait(false)),
                    payloadConflict);
                count++;
            }
            if (count != envelopes.Count) throw new InvalidDataException("PostgreSQL returned an incomplete command audit result.");
            await reader.DisposeAsync()
                .ConfigureAwait(false);
            // Cold-cache/restart compatibility: existing audit hashes remain hashes of
            // the complete original bytes. Compare normalized identity only on a retry.
            // A new statement also sees a competing INSERT committed after the CTE snapshot.
            for (var index = 0; index < results.Length; index++)
            {
                if (results[index].Accepted || envelopes[index].RetryCommandType is null) continue;
                var matches = await MatchesStoredRetryAsync(connection, transaction, envelopes[index], cancellationToken)
                .ConfigureAwait(false);
                results[index] = matches.HasValue ? new(false, false, !matches.Value) : new(false, true, false);
            }
            return results;
        }

        static async Task<bool?> MatchesStoredRetryAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
            CommandAuditEnvelope candidate, CancellationToken token)
        {
            var read = new NpgsqlCommand(EventSourceActorDbSql.CommandAuditPostgres_MatchesStoredRetryAsync_1, connection, transaction) { CommandTimeout = PostgresQueryTimeout.Default.CommandTimeoutSeconds };
            await using var readDisposal = read.ConfigureAwait(false);
            PostgresValueBinding.AddParameter(read, candidate.CommandId);
            var reader = await read.ExecuteReaderAsync(token)
                .ConfigureAwait(false);
            await using var readerDisposal = reader.ConfigureAwait(false);
            if (!await reader.ReadAsync(token)
                .ConfigureAwait(false)) return false;
            if (Enumerable.Range(0, 4).All(reader.IsDBNull)
                && candidate.RetryCommandType!.IsAssignableTo(typeof(TomasAI.IFM.Shared.EventSourcing.IAppendOnlyCommandRetryIdentity))) return null;
            if (Enumerable.Range(0, 4).Any(reader.IsDBNull)) return false;
            var bytes = (await reader.GetFieldValueAsync<byte[]>(0,token)
                .ConfigureAwait(false));
            var hash = (await reader.GetFieldValueAsync<byte[]>(3,token)
                .ConfigureAwait(false));
            if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(bytes), hash)) return false;
            var codec = new CommandAuditMessagePackCodec();
            var stored = codec.Deserialize(candidate.RetryCommandType!, bytes, reader.GetInt16(1), reader.GetInt32(2));
            // Fail closed if decoding under the candidate type would drop unknown fields
            // or otherwise reinterpret the original audit envelope.
            return stored is TomasAI.IFM.Shared.EventSourcing.ICommandRetryIdentity retry
                && codec.MatchesStoredEncoding(retry, bytes)
                && CryptographicOperations.FixedTimeEquals(codec.Serialize(retry.ForRetryIdentity()).Sha256, candidate.RetrySha256);
        }

        static void Add(NpgsqlCommand command, object value)
            => PostgresValueBinding.AddParameter(command, value);
    }

    /// <summary>Versioned command-log codec. Command audit payloads are deliberately never compressed.</summary>
    public sealed class CommandAuditMessagePackCodec
    {
        public const int CurrentVersion = 1;

        static readonly MessagePackSerializerOptions Options = MessagePackSerializerOptions.Standard
            .WithResolver(ContractlessStandardResolver.Instance)
            .WithCompression(MessagePackCompression.None);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        public CommandAuditPayload Serialize(ICommand command)
        {
            var buffer = new ArrayBufferWriter<byte>();
            MessagePackSerializer.Serialize(command.GetType(), buffer, command, Options);
            var bytes = buffer.WrittenSpan.ToArray();
            return new CommandAuditPayload(
                CommandAuditPayloadFormat.MessagePack,
                CurrentVersion,
                bytes,
                SHA256.HashData(bytes), TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Get(command));
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        public object Deserialize(Type commandType, ReadOnlyMemory<byte> payload, short format, int version,
            TomasAI.IFM.Shared.EventModelActor.MessageOrigin? origin = null)
        {
            if (!typeof(ICommand).IsAssignableFrom(commandType))
                throw new ArgumentException("The requested type is not a command.", nameof(commandType));
            if (format != (short)CommandAuditPayloadFormat.MessagePack || version != CurrentVersion)
                throw new InvalidDataException($"Unsupported command audit payload format/version {format}/{version}.");
            if (payload.IsEmpty) throw new InvalidDataException("Command audit payload is empty.");
            var value = MessagePackSerializer.Deserialize(commandType, payload, Options)
                ?? throw new InvalidDataException("Command audit payload produced no command.");
            return TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Restore(value, origin);
        }

        public bool Matches(ICommand command, ReadOnlySpan<byte> expectedHash)
        {
            if (expectedHash.Length != SHA256.HashSizeInBytes) return false;
            var candidate = Serialize(command);
            return CryptographicOperations.FixedTimeEquals(candidate.Sha256, expectedHash);
        }

        /// <summary>Checks exact stored fields, permitting only newly appended null fields.</summary>
        public bool MatchesStoredEncoding(ICommand command, ReadOnlyMemory<byte> stored)
        {
            var rewritten = Serialize(command).Bytes;
            if (stored.Span.SequenceEqual(rewritten)) return true;
            if (command is not IAppendOnlyCommandRetryIdentity) return false;
            var originalReader = new MessagePackReader(stored);
            var currentReader = new MessagePackReader(rewritten);
            var originalCount = originalReader.ReadArrayHeader();
            var currentCount = currentReader.ReadArrayHeader();
            if (originalCount > currentCount) return false;
            for (var index = 0; index < originalCount; index++)
            {
                var before = originalReader.ReadRaw();
                var after = currentReader.ReadRaw();
                if (!before.IsSingleSegment || !after.IsSingleSegment || !before.FirstSpan.SequenceEqual(after.FirstSpan)) return false;
            }
            for (var index = originalCount; index < currentCount; index++)
                if (!currentReader.TryReadNil()) return false;
            return originalReader.End && currentReader.End;
        }
    }

    public sealed class CommandAuditPersistenceOptions
    {
        public const string SectionName = "CommandAuditPersistence";

        public CommandAuditWriteMode WriteMode { get; set; } = CommandAuditWriteMode.WindowedMessagePack;
        public int QueueCommandCapacity { get; set; } = 8192;
        public int MaximumCommandsPerBatch { get; set; } = 64;
        public int MaximumBatchBytes { get; set; } = 256 * 1024;
        public int MaximumCommandPayloadBytes { get; set; } = 1024 * 1024;
        public TimeSpan MaximumOldestRequestDelay { get; set; } = TimeSpan.FromMilliseconds(1);
        public TimeSpan ShutdownDrainTimeout { get; set; } = TimeSpan.FromSeconds(10);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0015", Justification = "This existing options validator reports the invalid configuration property by name; preserve its established exception type and diagnostic property name.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3928", Justification = "This existing options validator reports the invalid configuration property by name; preserve its established exception type and diagnostic property name.")]
        public CommandAuditPersistenceOptions Validate()
        {
            if (!Enum.IsDefined(WriteMode)) throw new ArgumentOutOfRangeException(nameof(WriteMode));
            if (QueueCommandCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(QueueCommandCapacity));
            if (MaximumCommandsPerBatch <= 0) throw new ArgumentOutOfRangeException(nameof(MaximumCommandsPerBatch));
            if (MaximumBatchBytes <= 0) throw new ArgumentOutOfRangeException(nameof(MaximumBatchBytes));
            if (MaximumCommandPayloadBytes <= 0) throw new ArgumentOutOfRangeException(nameof(MaximumCommandPayloadBytes));
            if (MaximumOldestRequestDelay <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(MaximumOldestRequestDelay));
            if (ShutdownDrainTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(ShutdownDrainTimeout));
            return this;
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032", Justification = "This classified storage exception requires its diagnostic/business identity; preserve the existing constructor contract instead of permitting incomplete instances.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "RCS1194", Justification = "This classified storage exception requires its diagnostic/business identity; preserve the existing constructor contract instead of permitting incomplete instances.")]
    public sealed class CommandAuditDuplicateException(Guid commandId, bool hasCommittedEvents = false)
        : InvalidOperationException($"Command ID {commandId} already has a durable audit reservation.")
    {
        /// <summary>True only when the reservation has matching durable source events.</summary>
        public bool HasCommittedEvents { get; } = hasCommittedEvents;

        public Guid CommandId { get; } = commandId;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032", Justification = "This classified storage exception requires its diagnostic/business identity; preserve the existing constructor contract instead of permitting incomplete instances.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "RCS1194", Justification = "This classified storage exception requires its diagnostic/business identity; preserve the existing constructor contract instead of permitting incomplete instances.")]
    public sealed class CommandAuditPayloadConflictException(Guid commandId)
        : InvalidOperationException($"Command ID {commandId} is already associated with a different command payload.")
    {
        public Guid CommandId { get; } = commandId;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the established public storage/serialized schema contract and identifiers consumed by callers.")]
    public enum CommandAuditWriteMode : byte
    {
        SequentialJsonLegacy = 0,
        SequentialMessagePack = 1,
        WindowedMessagePack = 2
    }

    /// <summary>Reports whether a prepared command audit was newly reserved or already present.</summary>
    public enum CommandAuditReservationOutcome
    {
        Accepted,
        Existing,
        PayloadConflict
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1008", Justification = "Preserve the established public storage/serialized schema contract and identifiers consumed by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the established public storage/serialized schema contract and identifiers consumed by callers.")]
    public enum CommandAuditPayloadFormat : short
    {
        MessagePack = 1
    }

    /// <summary>
    /// Specifies the possible outcomes for a command execution.
    /// </summary>
    /// <remarks>Use this enumeration to determine the current state or final result of a command. The values indicate
    /// whether the command is still in progress, has completed successfully, has failed, or encountered a failure
    /// specifically during the denormalization process.</remarks>
    public enum CommandStatus
    {
        InProgress,
        Completed,
        Failed,
        DenormalizerFailed
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
    internal readonly record struct CommandAuditWriteResult(
        bool Accepted,
        bool LegacyConflict,
        bool PayloadConflict = false);

    public sealed record CommandAuditEnvelope(
        Guid CommandId,
        string StreamId,
        string ActorName,
        string CommandName,
        DateTime CommandTimestampUtc,
        CommandAuditPayload Payload,
        MessageOrigin? Origin = null)
    {
        public int PayloadBytes => Payload.Bytes.Length;
        internal Type? RetryCommandType { get; init; }
        public byte[] RetrySha256 { get; internal init; } = Payload.Sha256;

    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the established public storage/serialized schema contract and identifiers consumed by callers.")]
    public sealed record CommandAuditPayload(
        CommandAuditPayloadFormat Format,
        int Version,
        byte[] Bytes,
        byte[] Sha256,
        TomasAI.IFM.Shared.EventModelActor.MessageOrigin? Origin = null);
}

namespace TomasAI.IFM.Application.Storage.EventSourceDb.EventLog
{
    using System.Data;
    using System.Diagnostics;
    using System.Threading.Channels;
    using Npgsql;
    using TomasAI.IFM.Shared.Storage;
    using TomasAI.IFM.Framework.Storage.Postgres;
    using TomasAI.IFM.Shared.EventModelActor;
    using TomasAI.IFM.Shared.EventSourcing;
    using TomasAI.IFM.Shared.Exceptions;
    using TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;
    using TomasAI.IFM.Shared.EventProjector;
    using System.Diagnostics.Metrics;
    using System.Text.RegularExpressions;

    public sealed class BinaryCopyEventLogAppender : IEventLogAppender
    {
        const string CopySql = """
            COPY event_log
                (EventStreamId, EventNameId, EventVersion, StreamVersion, EventPayload, CommandId, EventTimestamp, MessageOriginJson)
            FROM STDIN (FORMAT BINARY)
            """;
        readonly string _connectionString;
        readonly string _copySql;
        readonly string _projectionSql;
        readonly bool _batchProjectionMarkers;
        readonly string _batchedProjectionSql;
        readonly EventLogPersistenceOptions _options;
        readonly EventLogMessagePackCodec _codec;
        readonly Channel<PendingAppend> _queue;
        readonly CancellationTokenSource _lifetime = new();
        readonly Task _consumer;
        readonly System.Diagnostics.TagList _metricTags;
        NpgsqlConnection? _connection;
        int _disposed;

        public BinaryCopyEventLogAppender(string connectionString, bool useLz4Compression, EventLogPersistenceOptions? options = null)
            : this(connectionString, useLz4Compression, CreateProductionConfiguration(options, useLz4Compression)) { }

        internal BinaryCopyEventLogAppender(string connectionString, bool useLz4Compression,
            EventLogPersistenceOptions? options, EventLogSqlLayout layout)
            : this(connectionString, useLz4Compression,
                new AppenderConfiguration((options ?? DefaultOptions(useLz4Compression)).Validate(), layout))
        { }

        BinaryCopyEventLogAppender(string connectionString, bool useLz4Compression, AppenderConfiguration configuration)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
            var layout = configuration.Layout;
            layout.ValidateConnection(connectionString);
            _copySql = layout.Resolve(CopySql);
            _projectionSql = layout.Resolve(EventSourceActorDbSql.TryCreateEventProjectorExecutionState);
            _batchProjectionMarkers = layout.BatchProjectionMarkers;
            _batchedProjectionSql = layout.Resolve(BatchedProjectionMarkerWriter.Sql);
            _connectionString = connectionString;
            _options = configuration.Options;
            UseLz4Compression = useLz4Compression;
            _codec = new EventLogMessagePackCodec(useLz4Compression);
            _metricTags = EventLogPersistenceMetrics.Tags(WriteMode, UseLz4Compression);
            _queue = Channel.CreateBounded<PendingAppend>(new BoundedChannelOptions(_options.QueueCommandCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
            _consumer = ConsumeAsync(_lifetime.Token);
        }

        static EventLogPersistenceOptions DefaultOptions(bool useLz4Compression) => new()
        {
            WriteMode = EventLogWriteMode.BinaryCopy,
            UseLz4Compression = useLz4Compression
        };

        static AppenderConfiguration CreateProductionConfiguration(EventLogPersistenceOptions? options, bool useLz4Compression)
        {
            var validated = (options ?? DefaultOptions(useLz4Compression)).Validate();
            return new(validated, EventLogSqlLayout.ForProduction(validated));
        }

        sealed record AppenderConfiguration(EventLogPersistenceOptions Options, EventLogSqlLayout Layout);

        public EventLogWriteMode WriteMode => EventLogWriteMode.BinaryCopy;
        public bool UseLz4Compression { get; }

        public async ValueTask<EventLogAppendResult> AppendAsync(EventLogAppendRequest request, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            var prepared = EventLogAppenderSupport.Prepare(request, _codec, _options);
            var pending = new PendingAppend(prepared);
            EventLogPersistenceMetrics.Queued(_metricTags);
            try
            {
                await _queue.Writer.WriteAsync(pending, cancellationToken)
                .ConfigureAwait(false);
            }
            catch
            {
                EventLogPersistenceMetrics.Dequeued(_metricTags);
                throw;
            }
            // Once admitted, the append receives a durable terminal result even if caller cancellation arrives later.
            return await pending.Completion.Task.ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Retain the existing bounded persistence failure boundary: complete cleanup or report failure while preserving ambiguous commit/recovery evidence and the original cause.")]
        async Task ConsumeAsync(CancellationToken cancellationToken)
        {
            PendingAppend? carry = null;
            try
            {
                while (carry is not null || await _queue.Reader.WaitToReadAsync(cancellationToken)
                .ConfigureAwait(false))
                {
                    var first = carry ?? (_queue.Reader.TryRead(out var queued) ? queued : null);
                    carry = null;
                    if (first is null) continue;
                    var batch = new List<PendingAppend> { first };
                    var eventCount = first.Request.Events.Count;
                    var byteCount = first.Request.PayloadBytes;
                    using var flushDelay = new CancellationTokenSource();
                    var delay = Task.Delay(_options.MaximumOldestRequestDelay, flushDelay.Token);

                    while (eventCount < _options.MaximumEventsPerBatch && byteCount < _options.MaximumBatchBytes)
                    {
                        if (_queue.Reader.TryRead(out var next))
                        {
                            var exceeds = batch.Count > 0 &&
                                (eventCount + next.Request.Events.Count > _options.MaximumEventsPerBatch ||
                                 byteCount + next.Request.PayloadBytes > _options.MaximumBatchBytes);
                            if (exceeds)
                            {
                                carry = next;
                                break;
                            }
                            batch.Add(next);
                            eventCount += next.Request.Events.Count;
                            byteCount += next.Request.PayloadBytes;
                            continue;
                        }

                        var data = _queue.Reader.WaitToReadAsync(cancellationToken).AsTask();
                        var completed = await Task.WhenAny(data, delay)
                .ConfigureAwait(false);
                        if (completed == delay) break;
                        if (!await data.ConfigureAwait(false)) break;
                    }
    await                flushDelay.CancelAsync()
                .ConfigureAwait(false);
                    await PersistBatchAsync(batch, cancellationToken)
                .ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                var failure = new OperationCanceledException("Binary event-log writer stopped before persistence completed.", cancellationToken);
                if (carry is not null) carry.Completion.TrySetException(failure);
                while (_queue.Reader.TryRead(out var pending)) pending.Completion.TrySetException(failure);
            }
            catch (Exception exception)
            {
                if (carry is not null) carry.Completion.TrySetException(exception);
                while (_queue.Reader.TryRead(out var pending)) pending.Completion.TrySetException(exception);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Retain the existing bounded persistence failure boundary: complete cleanup or report failure while preserving ambiguous commit/recovery evidence and the original cause.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
        async Task PersistBatchAsync(IReadOnlyList<PendingAppend> batch, CancellationToken cancellationToken,
            bool alreadyDequeued = false)
        {
            var started = Stopwatch.GetTimestamp();
            if (!alreadyDequeued)
                foreach (var _ in batch) EventLogPersistenceMetrics.Dequeued(_metricTags);
            try
            {
                var connection = await GetOpenConnectionAsync(cancellationToken)
                .ConfigureAwait(false);
                var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
                .ConfigureAwait(false);
            await using var transactionDisposal = transaction.ConfigureAwait(false);
                try
                {
                    var audited = batch.Where(static item => item.Request.CommandAudit is not null)
                        .Select(static item => item.Request.CommandAudit!).ToArray();
                    if (audited.Length > 0)
                    {
                        var reservations = await CommandAuditPostgres.ReserveAsync(
                            connection, transaction, audited, cancellationToken)
                .ConfigureAwait(false);
                        for (var index = 0; index < reservations.Length; index++)
                        {
                            if (reservations[index].PayloadConflict)
                                throw new CommandAuditPayloadConflictException(audited[index].CommandId);
                            if (!reservations[index].Accepted)
                                throw new CommandAuditDuplicateException(audited[index].CommandId);
                        }
                    }
                    var current = await LockStreamsAsync(connection, transaction, batch, cancellationToken)
                .ConfigureAwait(false);
                    var planned = Plan(batch, current);
                    await UpdateStreamVersionsAsync(connection, transaction, current, cancellationToken)
                .ConfigureAwait(false);
                    await ReserveEventIdsAsync(connection, transaction, planned, cancellationToken)
                .ConfigureAwait(false);
                    await CopyEventsAsync(connection, planned, cancellationToken)
                .ConfigureAwait(false);
                    List<ProjectionMarker>? markers = null;
                    foreach (var request in planned)
                    {
                        for (var index = 0; index < request.Pending.Request.Events.Count; index++)
                        {
                            var projection = request.Pending.Request.Events[index].RequiredProjection;
                            if (projection is not null)
                            {
                                if (_batchProjectionMarkers)
                                    (markers ??= []).Add(new ProjectionMarker(request.Assignments[index].EventVersion, projection));
                                else
                                    await EventLogAppenderSupport.InsertProjectionMarkerAsync(connection, transaction,
                                        request.Assignments[index].EventVersion, projection, cancellationToken, _projectionSql)
                .ConfigureAwait(false);
                            }
                        }
                    }
                    if (markers is not null)
                        await BatchedProjectionMarkerWriter.InsertAsync(connection, transaction, markers,
                            _batchedProjectionSql, cancellationToken)
                .ConfigureAwait(false);
                    try
                    {
                        await transaction.CommitAsync(cancellationToken)
                .ConfigureAwait(false);
                    }
                    catch (Exception exception)
                    {
                        throw new EventLogCommitOutcomeUnknownException(
                            $"PostgreSQL did not confirm an event-log batch commit containing {planned.Length} commands.", exception);
                    }
                    var committedAt = DateTime.UtcNow;
                    foreach (var request in planned)
                        request.Pending.Completion.TrySetResult(new EventLogAppendResult(request.Assignments, committedAt));
                    EventLogPersistenceMetrics.Committed(
                        _metricTags,
                        batch.Count,
                        batch.Sum(static item => item.Request.Events.Count),
                        batch.Sum(static item => (long)item.Request.PayloadBytes),
                        started);
                }
                catch
                {
                    try { await transaction.RollbackAsync(CancellationToken.None)
                .ConfigureAwait(false); } catch { /* Preserve the original database failure; cleanup is best effort. */ }
                    throw;
                }
            }
            catch (Exception exception)
            {
                EventLogPersistenceMetrics.Failed(_metricTags, started);
                if (_connection is not { FullState: ConnectionState.Open })
                {
                    if (_connection is not null) await _connection.DisposeAsync()
                .ConfigureAwait(false);
                    _connection = null;
                }
                // Audit rejection occurs before event writes/commit. The transaction has been rolled back.
                // Isolate requests so one duplicate/conflict cannot be reported as the outcome of another command.
                // Never retry transport failures or an unknown commit outcome here.
                if (batch.Count > 1 && exception is CommandAuditDuplicateException or CommandAuditPayloadConflictException)
                {
                    foreach (var pending in batch)
                        await PersistBatchAsync([pending], cancellationToken, alreadyDequeued: true)
                .ConfigureAwait(false);
                    return;
                }
                foreach (var pending in batch) pending.Completion.TrySetException(exception);
            }
        }

        async Task<NpgsqlConnection> GetOpenConnectionAsync(CancellationToken cancellationToken)
        {
            if (_connection is { FullState: ConnectionState.Open }) return _connection;
            if (_connection is not null) await _connection.DisposeAsync()
                .ConfigureAwait(false);
            _connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(_connectionString);
            await _connection.OpenAsync(cancellationToken)
                .ConfigureAwait(false);
            return _connection;
        }

        static async Task<Dictionary<long, long>> LockStreamsAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            IReadOnlyList<PendingAppend> batch,
            CancellationToken cancellationToken)
        {
            var ids = batch.Select(static request => request.Request.EventStreamId).Distinct().Order().ToArray();
            var command = EventLogAppenderSupport.Command(connection, transaction, EventSourceActorDbSql.BinaryCopyEventLogAppender_LockStreamsAsync_1);
            await using var commandDisposal = command.ConfigureAwait(false);
            EventLogAppenderSupport.Add(command, ids);
            var current = new Dictionary<long, long>(ids.Length);
            var reader = await command.ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);
            await using var readerDisposal = reader.ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken)
                .ConfigureAwait(false))
                current[Convert.ToInt64(reader.GetValue(0),System.Globalization.CultureInfo.InvariantCulture)] = reader.GetInt64(1);
            if (current.Count != ids.Length) throw new InvalidOperationException("An event stream disappeared before persistence.");
            return current;
        }

        static PlannedAppend[] Plan(IReadOnlyList<PendingAppend> batch, Dictionary<long, long> cursors)
        {
            var planned = new PlannedAppend[batch.Count];
            var ordered = batch
                .Select(static (pending, ordinal) => (Pending: pending, Ordinal: ordinal))
                .OrderBy(static item => item.Pending.Request.EventStreamId)
                .ThenBy(static item => item.Pending.Request.ExpectedStreamVersion ?? long.MaxValue)
                .ThenBy(static item => item.Ordinal)
                .ToArray();
            for (var requestIndex = 0; requestIndex < ordered.Length; requestIndex++)
            {
                var pending = ordered[requestIndex].Pending;
                var cursor = cursors[pending.Request.EventStreamId];
                if (pending.Request.ExpectedStreamVersion is long expected && expected != cursor)
                    throw new ConcurrencyException($"Event stream {pending.Request.EventStream} is no longer at expected version {expected}.");
                var assignments = new EventLogAssignment[pending.Request.Events.Count];
                for (var eventIndex = 0; eventIndex < assignments.Length; eventIndex++)
                    assignments[eventIndex] = new EventLogAssignment(0, ++cursor);
                cursors[pending.Request.EventStreamId] = cursor;
                planned[requestIndex] = new PlannedAppend(pending, assignments);
            }
            return planned;
        }

        static async Task UpdateStreamVersionsAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            IReadOnlyDictionary<long, long> finalVersions,
            CancellationToken cancellationToken)
        {
            var ordered = finalVersions.OrderBy(static pair => pair.Key).ToArray();
            var command = EventLogAppenderSupport.Command(connection, transaction, EventSourceActorDbSql.ReserveCopiedStreamVersions);
            await using var commandDisposal = command.ConfigureAwait(false);
            EventLogAppenderSupport.Add(command, ordered.Select(static pair => pair.Key).ToArray());
            EventLogAppenderSupport.Add(command, ordered.Select(static pair => pair.Value).ToArray());
            if (await command.ExecuteNonQueryAsync(cancellationToken)
                .ConfigureAwait(false) != ordered.Length)
                throw new InvalidOperationException("Not every event stream version was reserved.");
        }

        static async Task ReserveEventIdsAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            IReadOnlyList<PlannedAppend> planned,
            CancellationToken cancellationToken)
        {
            var count = planned.Sum(static request => request.Assignments.Length);
            var command = EventLogAppenderSupport.Command(connection, transaction,
                EventSourceActorDbSql.BinaryCopyEventLogAppender_ReserveEventIdsAsync_1);
            await using var commandDisposal = command.ConfigureAwait(false);
            EventLogAppenderSupport.Add(command, BindValues.Integer(count));
            var reader = await command.ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);
            await using var readerDisposal = reader.ConfigureAwait(false);
            foreach (var request in planned)
            {
                for (var index = 0; index < request.Assignments.Length; index++)
                {
                    if (!await reader.ReadAsync(cancellationToken)
                .ConfigureAwait(false))
                        throw new InvalidOperationException("PostgreSQL did not reserve every requested event ID.");
                    request.Assignments[index] = request.Assignments[index] with { EventVersion = reader.GetInt64(0) };
                    EventInitHelper.SetProperty(request.Pending.Request.Events[index].DomainEvent, nameof(IEvent.EventId),
                        request.Assignments[index].EventVersion);
                }
            }
        }

        async Task CopyEventsAsync(
            NpgsqlConnection connection,
            IReadOnlyList<PlannedAppend> planned,
            CancellationToken cancellationToken)
        {
            var importer = await connection.BeginBinaryImportAsync(_copySql, cancellationToken)
                .ConfigureAwait(false);
            await using var importerDisposal = importer.ConfigureAwait(false);
            foreach (var request in planned)
            {
                for (var index = 0; index < request.Pending.Request.Events.Count; index++)
                {
                    var source = request.Pending.Request;
                    var entry = source.Events[index];
                    var assignment = request.Assignments[index];
                    await PostgresValueBinding.WriteRowAsync(importer,
                        [source.EventStreamId, entry.EventNameId, assignment.EventVersion, assignment.StreamVersion,
                         entry.Payload, source.CommandId, $"{source.EventTimestampUtc:o}", entry.MessageOriginJson], cancellationToken)
                .ConfigureAwait(false);
                }
            }
            await importer.CompleteAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _queue.Writer.TryComplete();
            var timeout = Task.Delay(_options.ShutdownDrainTimeout);
            if (await Task.WhenAny(_consumer, timeout)
                .ConfigureAwait(false) != _consumer) await _lifetime.CancelAsync()
                .ConfigureAwait(false);
            try { await _consumer.ConfigureAwait(false); } catch (OperationCanceledException) { /* Cancellation is the expected shutdown outcome. */ }
            if (_connection is not null) await _connection.DisposeAsync()
                .ConfigureAwait(false);
            _lifetime.Dispose();
        }

        sealed class PendingAppend(PreparedEventLogRequest request)
        {
            internal PreparedEventLogRequest Request { get; } = request;
            internal TaskCompletionSource<EventLogAppendResult> Completion { get; } =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        sealed record PlannedAppend(PendingAppend Pending, EventLogAssignment[] Assignments);
    }

    public sealed class SequentialEventLogAppender : IEventLogAppender
    {
        const string InsertReturningVersion = EventSourceActorDbSql.InsertEventLog;
        const string InsertExpectedReturningEventVersion = EventSourceActorDbSql.InsertEventLogExpectedVersion;
        readonly EventLogPersistenceOptions _options;
        readonly EventLogMessagePackCodec _codec;
        readonly string _connectionString;
        readonly string _insertSql;
        readonly string _expectedInsertSql;
        readonly string _projectionSql;

        public SequentialEventLogAppender(string connectionString, bool useLz4Compression, EventLogPersistenceOptions? options = null)
            : this(connectionString, useLz4Compression, CreateProductionConfiguration(options, useLz4Compression)) { }

        internal SequentialEventLogAppender(string connectionString, bool useLz4Compression,
            EventLogPersistenceOptions? options, EventLogSqlLayout layout)
            : this(connectionString, useLz4Compression,
                new AppenderConfiguration((options ?? new EventLogPersistenceOptions { UseLz4Compression = useLz4Compression }).Validate(), layout))
        { }

        SequentialEventLogAppender(string connectionString, bool useLz4Compression, AppenderConfiguration configuration)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
            var layout = configuration.Layout;
            layout.ValidateConnection(connectionString);
            _insertSql = layout.Resolve(InsertReturningVersion);
            _expectedInsertSql = layout.Resolve(InsertExpectedReturningEventVersion);
            _projectionSql = layout.Resolve(EventSourceActorDbSql.TryCreateEventProjectorExecutionState);
            _options = configuration.Options;
            UseLz4Compression = useLz4Compression;
            _codec = new EventLogMessagePackCodec(useLz4Compression);
            var connectionOptions = new NpgsqlConnectionStringBuilder(connectionString);
            if (connectionOptions.MaxAutoPrepare == 0)
            {
                connectionOptions.MaxAutoPrepare = 16;
                connectionOptions.AutoPrepareMinUsages = 2;
            }
            _connectionString = connectionOptions.ConnectionString;
        }

        static AppenderConfiguration CreateProductionConfiguration(EventLogPersistenceOptions? options, bool useLz4Compression)
        {
            var validated = (options ?? new EventLogPersistenceOptions { UseLz4Compression = useLz4Compression }).Validate();
            return new(validated, EventLogSqlLayout.ForProduction(validated));
        }

        sealed record AppenderConfiguration(EventLogPersistenceOptions Options, EventLogSqlLayout Layout);

        public EventLogWriteMode WriteMode => EventLogWriteMode.Sequential;
        public bool UseLz4Compression { get; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Retain the existing bounded persistence failure boundary: complete cleanup or report failure while preserving ambiguous commit/recovery evidence and the original cause.")]
        public async ValueTask<EventLogAppendResult> AppendAsync(EventLogAppendRequest request, CancellationToken cancellationToken = default)
        {
            var started = Stopwatch.GetTimestamp();
            var tags = EventLogPersistenceMetrics.Tags(WriteMode, UseLz4Compression);
            var prepared = EventLogAppenderSupport.Prepare(request, _codec, _options);
            if (prepared.Events.Count == 1 && prepared.Events[0].RequiredProjection is null)
                return await AppendSingleAutocommitAsync(prepared, tags, started, cancellationToken)
                .ConfigureAwait(false);

            var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(_connectionString);
            await using var connectionDisposal = connection.ConfigureAwait(false);
            await connection.OpenAsync(cancellationToken)
                .ConfigureAwait(false);
            var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
                .ConfigureAwait(false);
            await using var transactionDisposal = transaction.ConfigureAwait(false);
            var assignments = new EventLogAssignment[prepared.Events.Count];
            try
            {
                for (var index = 0; index < prepared.Events.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var entry = prepared.Events[index];
                    var expected = prepared.ExpectedStreamVersion.HasValue ? prepared.ExpectedStreamVersion.Value + index : (long?)null;
                    assignments[index] = await InsertAsync(connection, transaction, prepared, entry, expected, cancellationToken)
                .ConfigureAwait(false);
                    EventInitHelper.SetProperty(entry.DomainEvent, nameof(IEvent.EventId), assignments[index].EventVersion);
                    if (entry.RequiredProjection is not null)
                        await EventLogAppenderSupport.InsertProjectionMarkerAsync(connection, transaction, assignments[index].EventVersion, entry.RequiredProjection, cancellationToken, _projectionSql)
                .ConfigureAwait(false);
                }
                try
                {
                    await transaction.CommitAsync(cancellationToken)
                .ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    throw new EventLogCommitOutcomeUnknownException(
                        $"PostgreSQL did not confirm the event-log commit for command {prepared.CommandId}.", exception);
                }
                EventLogPersistenceMetrics.Committed(tags, 1, prepared.Events.Count, prepared.PayloadBytes, started);
                return new EventLogAppendResult(assignments, DateTime.UtcNow);
            }
            catch
            {
                EventLogPersistenceMetrics.Failed(tags, started);
                try { await transaction.RollbackAsync(CancellationToken.None)
                .ConfigureAwait(false); } catch { /* Preserve the original database failure; cleanup is best effort. */ }
                throw;
            }
        }

        async ValueTask<EventLogAppendResult> AppendSingleAutocommitAsync(
            PreparedEventLogRequest prepared,
            System.Diagnostics.TagList tags,
            long started,
            CancellationToken cancellationToken)
        {
            NpgsqlConnection connection;
            try
            {
                connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(_connectionString);
                await connection.OpenAsync(cancellationToken)
                .ConfigureAwait(false);
            }
            catch
            {
                EventLogPersistenceMetrics.Failed(tags, started);
                throw;
            }

            await using (connection.ConfigureAwait(false))
            {
                try
                {
                    var assignment = await InsertAsync(
                        connection, null, prepared, prepared.Events[0], prepared.ExpectedStreamVersion, cancellationToken)
                .ConfigureAwait(false);
                    EventInitHelper.SetProperty(prepared.Events[0].DomainEvent, nameof(IEvent.EventId), assignment.EventVersion);
                    EventLogPersistenceMetrics.Committed(tags, 1, 1, prepared.PayloadBytes, started);
                    return new EventLogAppendResult([assignment], DateTime.UtcNow);
                }
                catch (NpgsqlException exception) when (exception is not PostgresException)
                {
                    EventLogPersistenceMetrics.Failed(tags, started);
                    throw new EventLogCommitOutcomeUnknownException(
                        $"PostgreSQL did not confirm the event-log statement for command {prepared.CommandId}.", exception);
                }
                catch
                {
                    EventLogPersistenceMetrics.Failed(tags, started);
                    throw;
                }
            }
        }

        async Task<EventLogAssignment> InsertAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction? transaction,
            PreparedEventLogRequest request,
            PreparedEventLogEntry entry,
            long? expectedStreamVersion,
            CancellationToken cancellationToken)
        {
            if (expectedStreamVersion.HasValue)
            {
                var expectedCommand = EventLogAppenderSupport.Command(
                    connection, transaction, _expectedInsertSql);
                await using var expectedCommandDisposal = expectedCommand.ConfigureAwait(false);
                var parameters = new InsertEventLogExpectedVersion(request.EventStreamId, entry.EventNameId, entry.Payload,
                    request.CommandId, $"{request.EventTimestampUtc:o}", expectedStreamVersion.Value, entry.MessageOriginJson);
                PostgresValueBinding.AddParameters(expectedCommand, (object?[])parameters.Bind());
                var scalar = await expectedCommand.ExecuteScalarAsync(cancellationToken)
                .ConfigureAwait(false);
                if (scalar is not long eventVersion)
                    throw new ConcurrencyException(
                        $"Event stream {request.EventStream} is no longer at expected version {expectedStreamVersion}.");
                return new EventLogAssignment(eventVersion, expectedStreamVersion.Value + 1);
            }

            var command = EventLogAppenderSupport.Command(
                connection,
                transaction,
                _insertSql);
            await using var commandDisposal = command.ConfigureAwait(false);
            var insertParameters = new InsertEventLog(request.EventStreamId, entry.EventNameId, entry.Payload,
                request.CommandId, $"{request.EventTimestampUtc:o}", entry.MessageOriginJson);
            PostgresValueBinding.AddParameters(command, (object?[])insertParameters.Bind());
            var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken)
                .ConfigureAwait(false);
            await using var readerDisposal = reader.ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken)
                .ConfigureAwait(false))
                throw new InvalidOperationException($"Event stream {request.EventStream} was not found.");
            return new EventLogAssignment(reader.GetInt64(0), reader.GetInt64(1));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    internal static class EventLogAppenderSupport
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3236", Justification = "The validated member belongs to this public input argument; explicitly report its root parameter rather than exposing a member expression as the parameter name.")]
        internal static PreparedEventLogRequest Prepare(
            EventLogAppendRequest request,
            EventLogMessagePackCodec codec,
            EventLogPersistenceOptions options)
        {
            if (request.Events is null || request.Events.Count == 0) throw new ArgumentException("At least one event is required.", nameof(request));
            if (request.Events.Count > options.MaximumEventsPerCommand) throw new ArgumentException("Event command exceeds its configured event limit.", nameof(request));

            var prepared = new PreparedEventLogEntry[request.Events.Count];
            var totalBytes = 0;
            for (var index = 0; index < request.Events.Count; index++)
            {
                var item = request.Events[index] ?? throw new ArgumentException("Event entry cannot be null.", nameof(request));
                var payload = codec.Serialize(item.DomainEvent);
                if (payload.Length == 0 || payload.Length > options.MaximumEventPayloadBytes)
                    throw new ArgumentException("Event payload is outside its configured size limit.", nameof(request));
                totalBytes = checked(totalBytes + payload.Length);
                if (totalBytes > options.MaximumCommandPayloadBytes)
                    throw new ArgumentException("Event command exceeds its configured payload limit.", nameof(request));
                DurableProjectionRequirement? projection = null;
                if (item.DomainEvent is IRequireDurableProjection { RequiresDurableProjection: true } required)
                    projection = required.RequiredProjection;
                prepared[index] = new PreparedEventLogEntry(item.EventNameId, item.DomainEvent, payload, projection,
                    TomasAI.IFM.Shared.EventModelActor.MessageOriginJson.Serialize(
                        TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Get(item.DomainEvent)));
            }
            return new PreparedEventLogRequest(request.EventStream, request.EventStreamId, request.CommandId, prepared,
                request.ExpectedStreamVersion, DateTime.SpecifyKind(request.EventTimestampUtc, DateTimeKind.Utc), totalBytes,
                request.CommandAudit);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2100", Justification = "This storage executor accepts application SQL from reviewed schema/query contracts; runtime values are sent as bound parameters rather than interpolated into SQL.")]
        internal static NpgsqlCommand Command(NpgsqlConnection connection, NpgsqlTransaction? transaction, string sql)
            => new(sql, connection, transaction) { CommandTimeout = PostgresQueryTimeout.Default.CommandTimeoutSeconds };

        internal static void Add(NpgsqlCommand command, object value)
            => PostgresValueBinding.AddParameter(command, value);

        internal static async Task InsertProjectionMarkerAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            long eventId,
            DurableProjectionRequirement requirement,
            CancellationToken cancellationToken,
            string? sql = null)
        {
            var started = Stopwatch.GetTimestamp();
            var now = DateTime.UtcNow;
            var command = Command(connection, transaction, sql ?? EventSourceActorDbSql.TryCreateEventProjectorExecutionState);
            await using var commandDisposal = command.ConfigureAwait(false);
            Add(command, BindValues.Bigint(eventId));
            Add(command, BindValues.Text(requirement.ActorName));
            Add(command, BindValues.Text(requirement.ProjectorName));
            Add(command, BindValues.Boolean(false));
            Add(command, BindValues.Integer(0));
            Add(command, BindValues.Text("Processing"));
            Add(command, BindValues.Text(requirement.InitialStage.ToString()));
            Add(command, BindValues.Text(string.Empty));
            Add(command, BindValues.Text($"{now:o}"));
            Add(command, BindValues.Text($"{now:o}"));
            Add(command, BindValues.TimestampTz(now));
            if (await command.ExecuteScalarAsync(cancellationToken)
                .ConfigureAwait(false) is null)
                throw new InvalidOperationException("The required durable projection marker was not persisted.");
            EventLogPersistenceMetrics.ProjectionMarkersWritten(1, started);
        }
    }

    /// <summary>Experimental set-based equivalent of TryCreateEventProjectorExecutionState.
    /// Only enabled through the guarded benchmark layout; caller owns commit/rollback.</summary>
    internal static class BatchedProjectionMarkerWriter
    {
        internal const string Sql = EventSourceActorDbSql.BatchedProjectionMarkerWriter_Statement_1;

        internal static async Task InsertAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
            IReadOnlyList<ProjectionMarker> markers, string sql, CancellationToken cancellationToken)
        {
            if (markers.Count == 0) return;
            var started = Stopwatch.GetTimestamp();
            var ids = new long[markers.Count];
            var actors = new string[markers.Count];
            var projectors = new string[markers.Count];
            var stages = new string[markers.Count];
            for (var i = 0; i < markers.Count; i++)
            {
                ids[i] = markers[i].EventId;
                actors[i] = markers[i].Requirement.ActorName;
                projectors[i] = markers[i].Requirement.ProjectorName;
                stages[i] = markers[i].Requirement.InitialStage.ToString();
            }
            var now = DateTime.UtcNow;
            var command = EventLogAppenderSupport.Command(connection, transaction, sql);
            await using var commandDisposal = command.ConfigureAwait(false);
            EventLogAppenderSupport.Add(command, ids);
            EventLogAppenderSupport.Add(command, actors);
            EventLogAppenderSupport.Add(command, projectors);
            EventLogAppenderSupport.Add(command, stages);
            EventLogAppenderSupport.Add(command, BindValues.Text($"{now:o}"));
            EventLogAppenderSupport.Add(command, BindValues.TimestampTz(now));
            var inserted = await command.ExecuteScalarAsync(cancellationToken)
                .ConfigureAwait(false);
            if (inserted is not long count || count != markers.Count)
                throw new InvalidOperationException("Not every required durable projection marker was persisted.");
            EventLogPersistenceMetrics.ProjectionMarkersWritten(markers.Count, started);
        }
    }

    internal static class EventLogPersistenceMetrics
    {
        internal const string MeterName = "TomasAI.IFM.EventLogPersistence";
        static readonly Meter Meter = new(MeterName);
        static readonly Counter<long> Commands = Meter.CreateCounter<long>("ifm.event_log.append.commands");
        static readonly Counter<long> Events = Meter.CreateCounter<long>("ifm.event_log.append.events");
        static readonly Counter<long> PayloadBytes = Meter.CreateCounter<long>("ifm.event_log.append.payload_bytes", "By");
        static readonly Counter<long> Failures = Meter.CreateCounter<long>("ifm.event_log.append.failures");
        static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("ifm.event_log.append.duration", "ms");
        static readonly Histogram<long> BatchCommands = Meter.CreateHistogram<long>("ifm.event_log.batch.commands");
        static readonly Histogram<long> BatchEvents = Meter.CreateHistogram<long>("ifm.event_log.batch.events");
        static readonly UpDownCounter<long> QueueDepth = Meter.CreateUpDownCounter<long>("ifm.event_log.queue.depth");

        internal static TagList Tags(EventLogWriteMode mode, bool compression)
        {
            var tags = new TagList();
            tags.Add("write_mode", mode.ToString());
            tags.Add("lz4", compression);
            return tags;
        }

        static readonly Counter<long> MarkerCommands = Meter.CreateCounter<long>("ifm.event_log.marker.commands");
        static readonly Counter<long> MarkerRows = Meter.CreateCounter<long>("ifm.event_log.marker.rows");
        static readonly Histogram<double> MarkerDuration = Meter.CreateHistogram<double>("ifm.event_log.marker.duration", "ms");

        internal static void ProjectionMarkersWritten(int rows, long started)
        {
            MarkerCommands.Add(1);
            MarkerRows.Add(rows);
            MarkerDuration.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }

        internal static void Queued(TagList tags) => QueueDepth.Add(1, tags);
        internal static void Dequeued(TagList tags) => QueueDepth.Add(-1, tags);

        internal static void Committed(
            TagList tags,
            int commandCount,
            int eventCount,
            long payloadBytes,
            long startedTimestamp)
        {
            Commands.Add(commandCount, tags);
            Events.Add(eventCount, tags);
            PayloadBytes.Add(payloadBytes, tags);
            BatchCommands.Record(commandCount, tags);
            BatchEvents.Record(eventCount, tags);
            Duration.Record(Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds, tags);
        }

        internal static void Failed(TagList tags, long startedTimestamp)
        {
            Failures.Add(1, tags);
            Duration.Record(Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds, tags);
        }
    }

    /// <summary>Immutable, allowlisted event-log SQL routing. Benchmark-only behavior has a separate guarded factory.</summary>
    internal sealed class EventLogSqlLayout
    {
        internal static EventLogSqlLayout Current { get; } = new();
        readonly bool _benchmark;
        internal bool BatchProjectionMarkers { get; }
        EventLogSqlLayout(bool batchProjectionMarkers = false, bool benchmark = false)
            => (BatchProjectionMarkers, _benchmark) = (batchProjectionMarkers, benchmark);

        internal static EventLogSqlLayout ForProduction(EventLogPersistenceOptions options)
        {
            options.Validate();
            return Current;
        }

        internal static EventLogSqlLayout ForBenchmark(string connectionString, bool batchProjectionMarkers = false)
        {
            var connection = new NpgsqlConnectionStringBuilder(connectionString);
            if (connection.Host is not ("localhost" or "127.0.0.1" or "::1") ||
                !Regex.IsMatch(connection.Database ?? "", @"\Aifm_eventlog_bench_[a-f0-9]{12}_[a-z0-9_]+\z", RegexOptions.NonBacktracking))
                throw new ArgumentException("Benchmark layouts require a loopback-only, uniquely named benchmark database.", nameof(connectionString));
            return new EventLogSqlLayout(batchProjectionMarkers, benchmark: true);
        }

        internal void ValidateConnection(string connectionString)
        {
            // A benchmark layout must never be reusable against an application database.
            if (_benchmark) _ = ForBenchmark(connectionString, BatchProjectionMarkers);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal string Resolve(string sql) => sql;
    }

    public sealed class EventLogPersistenceOptions
    {
        public const string SectionName = "EventLogPersistence";
        public EventLogWriteMode WriteMode { get; set; } = EventLogWriteMode.Sequential;
        public bool UseLz4Compression { get; set; }
        public int QueueCommandCapacity { get; set; } = 8192;
        public int MaximumEventsPerBatch { get; set; } = 256;
        public int MaximumBatchBytes { get; set; } = 1024 * 1024;
        public TimeSpan MaximumOldestRequestDelay { get; set; } = TimeSpan.FromMilliseconds(1);
        public int MaximumEventsPerCommand { get; set; } = 4096;
        public int MaximumEventPayloadBytes { get; set; } = 16 * 1024 * 1024;
        public int MaximumCommandPayloadBytes { get; set; } = 32 * 1024 * 1024;
        public TimeSpan ShutdownDrainTimeout { get; set; } = TimeSpan.FromSeconds(10);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0015", Justification = "This existing options validator reports the invalid configuration property by name; preserve its established exception type and diagnostic property name.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3928", Justification = "This existing options validator reports the invalid configuration property by name; preserve its established exception type and diagnostic property name.")]
        public EventLogPersistenceOptions Validate()
        {
            if (!Enum.IsDefined(WriteMode)) throw new ArgumentOutOfRangeException(nameof(WriteMode));
            if (QueueCommandCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(QueueCommandCapacity));
            if (MaximumEventsPerBatch <= 0) throw new ArgumentOutOfRangeException(nameof(MaximumEventsPerBatch));
            if (MaximumBatchBytes <= 0) throw new ArgumentOutOfRangeException(nameof(MaximumBatchBytes));
            if (MaximumOldestRequestDelay <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(MaximumOldestRequestDelay));
            if (MaximumEventsPerCommand <= 0) throw new ArgumentOutOfRangeException(nameof(MaximumEventsPerCommand));
            if (MaximumEventPayloadBytes <= 0) throw new ArgumentOutOfRangeException(nameof(MaximumEventPayloadBytes));
            if (MaximumCommandPayloadBytes < MaximumEventPayloadBytes) throw new ArgumentOutOfRangeException(nameof(MaximumCommandPayloadBytes));
            if (ShutdownDrainTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(ShutdownDrainTimeout));
            return this;
        }
    }

    /// <summary>
    /// Reports that PostgreSQL did not provide a definitive commit outcome. Callers must reconcile durable state by
    /// command ID and must not blindly retry the append.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032", Justification = "This classified storage exception requires its diagnostic/business identity; preserve the existing constructor contract instead of permitting incomplete instances.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "RCS1194", Justification = "This classified storage exception requires its diagnostic/business identity; preserve the existing constructor contract instead of permitting incomplete instances.")]
    public sealed class EventLogCommitOutcomeUnknownException(string message, Exception innerException)
        : Exception(message, innerException);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the established public storage/serialized schema contract and identifiers consumed by callers.")]
    public enum EventLogWriteMode : byte
    {
        Sequential = 0,
        BinaryCopy = 1
    }

    internal sealed record PreparedEventLogRequest(
        string EventStream,
        long EventStreamId,
        Guid CommandId,
        IReadOnlyList<PreparedEventLogEntry> Events,
        long? ExpectedStreamVersion,
        DateTime EventTimestampUtc,
        int PayloadBytes,
        CommandAuditEnvelope? CommandAudit);

    internal sealed record PreparedEventLogEntry(
        int EventNameId,
        IEvent DomainEvent,
        byte[] Payload,
        DurableProjectionRequirement? RequiredProjection,
        string? MessageOriginJson = null);

    public sealed record EventLogAppendRequest(
        EventLogBatchIdentity Identity,
        IReadOnlyList<EventLogAppendEntry> Events,
        DateTime EventTimestampUtc,
        CommandAuditEnvelope? CommandAudit = null)
    {
        public EventLogAppendRequest(string eventStream, long eventStreamId, Guid commandId,
            IReadOnlyList<EventLogAppendEntry> events, long? expectedStreamVersion, DateTime eventTimestampUtc,
            CommandAuditEnvelope? commandAudit = null)
            : this(new EventLogBatchIdentity(eventStream, eventStreamId, commandId, expectedStreamVersion),
                events, eventTimestampUtc, commandAudit)
        {
        }

        public string EventStream => Identity.Stream;
        public long EventStreamId => Identity.StreamId;
        public Guid CommandId => Identity.CommandId;
        public long? ExpectedStreamVersion => Identity.ExpectedStreamVersion;
    }

    public sealed record EventLogAppendResult(IReadOnlyList<EventLogAssignment> Assignments, DateTime CommittedAtUtc);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
    public readonly record struct EventLogAssignment(long EventVersion, long StreamVersion);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
    internal readonly record struct ProjectionMarker(long EventId, DurableProjectionRequirement Requirement);
}
