using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector.ReadModels;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.Storage;


using TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;
using TomasAI.IFM.Application.Storage.EventSourceDb.EventLog;
using TomasAI.IFM.Shared.EventModelActor;

using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using TomasAI.IFM.Shared.EventProjector;


namespace TomasAI.IFM.Application.Storage.EventSourceDb;

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
            return await context.Database.Use(nameof(EventSourceDbSql.GetResidentStreamHead), EventSourceDbSql.GetResidentStreamHead)
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
                .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.TryInsertCommandLog)}", EventSourceDbSql.TryInsertCommandLog)
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
                    .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventLogByEventStreamId)}", context.EventLogSql(EventSourceDbSql.GetEventLogByEventStreamId))
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
                .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventStreamId)}", EventSourceDbSql.GetEventStreamId)
            .SetParameters(preparedParameters2)
            .ExecuteSingleAsync<EventStreamIdReadModel>(EventSourceActorDbContext.MapToEventStreamId, cancellationToken)
            .ConfigureAwait(false);
            if (eventStreamIdModel is not null)
                return eventStreamIdModel.EventStreamId;
        var preparedParameters3 = new InsertEventStreamId(eventStream);

            return await (context._dbFactory.ActorEventSourceDb
                .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.InsertEventStreamId)}", EventSourceDbSql.InsertEventStreamId)
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
                      .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.InsertEventNameId)}", EventSourceDbSql.InsertEventNameId)
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
                .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventNameId)}", EventSourceDbSql.GetEventNameId)
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
                .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventLogLastNRange)}", context.EventLogSql(EventSourceDbSql.GetEventLogLastNRange))
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
            var sql = components ? EventSourceDbSql.GetEventLogFromLatestComponentSnapshots
                : stateless ? EventSourceDbSql.GetStatelessEventLogFromCreationSnapshot
                : checkpointIdentities is not null ? EventSourceDbSql.GetEventLogFromCreationWithCheckpointIdentities
                : checkpointNames is null ? EventSourceDbSql.GetEventLogFromCreationSnapshot : EventSourceDbSql.GetEventLogFromCreationWithCheckpoint;
            var statementName = components ? nameof(EventSourceDbSql.GetEventLogFromLatestComponentSnapshots)
                : stateless ? nameof(EventSourceDbSql.GetStatelessEventLogFromCreationSnapshot)
                : checkpointIdentities is not null ? nameof(EventSourceDbSql.GetEventLogFromCreationWithCheckpointIdentities)
                : checkpointNames is null ? nameof(EventSourceDbSql.GetEventLogFromCreationSnapshot)
                : nameof(EventSourceDbSql.GetEventLogFromCreationWithCheckpoint);
            var query = context._dbFactory.ActorEventSourceDb.Use($"{nameof(EventSourceDbSql)}.{statementName}", context.EventLogSql(sql));
            if (checkpointIdentities is not null)
                query.SetParameters(new GetCheckpointEventLogByIdentity(eventStreamId, checkpointIdentities));
            else if (checkpointNames is null)
                query.SetParameters(new GetEventLogByEventStreamId(eventStreamId));
            else
                query.SetParameters(new GetMaxEventVersion(eventStreamId, checkpointNames));
            await query.ExecuteMapReduceAsync(MapCreationRow, ReduceAndValidate, cancellationToken)
            .ConfigureAwait(false);
            if (replayedVersion != head)
                throw new InvalidDataException($"Creation snapshot replay for stream {eventStreamId} ended at {replayedVersion}, expected {head}.");

            void ReduceAndValidate(IEnumerable<EventStreamReadModel> rows)
            {
                using var iterator = rows.GetEnumerator();
                reducerAction(ReadRows());
                // Validate the remaining rows even when a reducer stops reading early.
                while (iterator.MoveNext()) { }

                IEnumerable<EventStreamReadModel> ReadRows()
                {
                    while (iterator.MoveNext())
                        if (iterator.Current.EventVersion > 0)
                            yield return iterator.Current;
                }
            }

            EventStreamReadModel MapCreationRow(IObjectDataRecord record)
            {
                head = record.GetLong(8);
                if (record.IsNull(3))
                {
                    if (head != 0)
                        throw new InvalidDataException($"Creation snapshot is missing for nonempty stream {eventStreamId}.");
                    return new EventStreamReadModel();
                }
                var version = record.GetLong(7);
                var isCheckpoint = components
                    ? replayedVersion >= 1 && version > replayedVersion && version == record.GetLong(9)
                    : (stateless || checkpointNames is not null || checkpointIdentities is not null) && replayedVersion == 1 && version == record.GetLong(9);
                if (version != replayedVersion + 1 && !isCheckpoint)
                    throw new InvalidDataException($"Creation snapshot replay gap in stream {eventStreamId}: {replayedVersion} -> {version}.");
                replayedVersion = version;
                return new EventStreamReadModel
                {
                    EventVersion = record.GetLong(3), EventTypeName = record.GetString(2),
                    EventData = record.GetBytes(4), StreamVersion = version,
                    Origin = EventSourceActorDbContext.MapToOrigin(record)
                };
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
                .Use(nameof(EventSourceDbSql.GetCompatibleEventNameIds), EventSourceDbSql.GetCompatibleEventNameIds)
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
            => transaction.QueryAsync(EventSourceDbSql.ReadMaintenanceSourceEvents, (object? [])new MaintenanceSourceStreams(exactStreams, prefixes).Bind(), EventSourceActorDbContext.MapToMaintenanceSource, token);

        /// <summary>Checks for committed history matching the supplied prefixes and exclusions.</summary>
        internal async Task<bool> HasCommittedStreamWithPrefixAsync(string[] excludedStreams, string[] prefixes, CancellationToken token)
            => await transaction.ScalarAsync(EventSourceDbSql.HasCommittedStreamWithPrefix, (object? [])new MaintenanceSourceStreams(excludedStreams, prefixes).Bind(), token)
            .ConfigureAwait(false) is true;

        /// <summary>Reports whether a stream has committed source history without creating metadata.</summary>
        internal async Task<bool> HasCommittedMaintenanceStreamAsync(string stream, CancellationToken token)
            => await transaction.ScalarAsync(EventSourceDbSql.HasCommittedMaintenanceStream, (object? [])new MaintenanceSourceStream(stream).Bind(), token)
            .ConfigureAwait(false) is not null;
    }
}
