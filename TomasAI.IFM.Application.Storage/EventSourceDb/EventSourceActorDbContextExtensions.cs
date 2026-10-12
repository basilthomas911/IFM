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

/// <summary>Internal persistence helpers and validation for the actor event-source context.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "C# 14 instance extensions retain their context receiver; static conversion changes the storage helper API.")]
internal static class EventSourceActorDbContextExtensions
{
    extension(EventSourceActorDbContext context)
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Argument validation belongs to the context extension API.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3236", Justification = "Forward the original operation parameter name rather than the helper parameter name.")]
        internal void ValidateRequiredArgument([System.Diagnostics.CodeAnalysis.NotNull] object? argument, string parameterName)
            => ArgumentNullException.ThrowIfNull(argument, parameterName);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Argument validation belongs to the context extension API.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3236", Justification = "Forward the original operation parameter name rather than the helper parameter name.")]
        internal void ValidateTextArgument([System.Diagnostics.CodeAnalysis.NotNull] string? argument, string parameterName)
            => ArgumentException.ThrowIfNullOrWhiteSpace(argument, parameterName);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Argument validation belongs to the context extension API.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3236", Justification = "Forward the original operation parameter name rather than the helper parameter name.")]
        internal void ValidateNonnegativeArgument(long argument, string parameterName)
            => ArgumentOutOfRangeException.ThrowIfLessThan(argument, 0, parameterName);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Argument validation belongs to the context extension API.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3236", Justification = "Forward the original operation parameter name rather than the helper parameter name.")]
        internal void ValidatePositiveArgument(long argument, string parameterName)
            => ArgumentOutOfRangeException.ThrowIfNegativeOrZero(argument, parameterName);

        internal string EventLogSql(string sql) => context._eventLogSqlLayout.Resolve(sql);

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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Validation belongs to the context extension API.")]
        internal void ValidateProjectorIdentity(long eventId, string projectorName)
        {
            if (eventId <= 0)
                throw new ArgumentOutOfRangeException(nameof(eventId), eventId, "The event id must be positive.");
            ArgumentException.ThrowIfNullOrWhiteSpace(projectorName);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Validation belongs to the context extension API.")]
        internal void ValidateExecutionToken(Guid executionToken)
        {
            if (executionToken == Guid.Empty)
                throw new ArgumentException("The execution token cannot be empty.", nameof(executionToken));
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Validation belongs to the context extension API.")]
        internal DateTime RequireUtc(DateTime value, string parameterName)
        {
            if (value.Kind != DateTimeKind.Utc)
                throw new ArgumentException("The timestamp must use DateTimeKind.Utc.", parameterName);
            return value;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Validation belongs to the context extension API.")]
        internal DateTime? ValidateOptionalUtc(DateTime? value, string parameterName)
            => value.HasValue ? context.RequireUtc(value.Value, parameterName) : null;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Validation belongs to the context extension API.")]
        internal DateTime GetLeaseExpiry(DateTime nowUtc, TimeSpan leaseDuration)
        {
            context.RequireUtc(nowUtc, nameof(nowUtc));
            if (leaseDuration <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(leaseDuration), leaseDuration, "The lease duration must be positive.");
            return nowUtc.Add(leaseDuration);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Validation belongs to the context extension API.")]
        internal void ValidateTransition(EventProjectorStateTransition transition, bool terminal)
        {
            ArgumentNullException.ThrowIfNull(transition);
            context.ValidateProjectorIdentity(transition.EventId, transition.ProjectorName);
            context.ValidateExecutionToken(transition.ExecutionToken);
            if (transition.ExpectedRevision < 0)
                throw new ArgumentOutOfRangeException(nameof(transition));
            if (transition.ExpectedStage == EventProjectorStageType.Completed)
                throw new ArgumentOutOfRangeException(nameof(transition), "A completed execution cannot transition again.");
            if (transition.RetryCount < 0)
                throw new ArgumentOutOfRangeException(nameof(transition));

            context.ValidateOptionalUtc(transition.NextAttemptAtUtc, nameof(transition.NextAttemptAtUtc));
            context.ValidateOptionalUtc(transition.LastErrorAtUtc, nameof(transition.LastErrorAtUtc));

            if (terminal)
            {
                if (transition.Outcome is EventProjectorOutcomeType.Processing or EventProjectorOutcomeType.Retrying)
                    throw new ArgumentOutOfRangeException(nameof(transition), "A terminal transition requires a terminal outcome.");
            }
            else
            {
                if (transition.NextStage == EventProjectorStageType.Completed)
                    throw new ArgumentOutOfRangeException(nameof(transition), "Use terminalization to complete an execution.");
                if (transition.Outcome is not (EventProjectorOutcomeType.Processing or EventProjectorOutcomeType.Retrying))
                    throw new ArgumentOutOfRangeException(nameof(transition), "A non-terminal transition requires an active outcome.");
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3236", Justification = "The validated member belongs to this public input argument; explicitly report its root parameter rather than exposing a member expression as the parameter name.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Validation belongs to the context extension API.")]
        internal void ValidateOutboxMessage(
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
            ArgumentException.ThrowIfNullOrWhiteSpace(message.EventTypeName, nameof(message));
            ArgumentNullException.ThrowIfNull(message.EventPayload, nameof(message));
            if (message.EventPayload.Length == 0)
                throw new ArgumentException("The outbox payload cannot be empty.", nameof(message));
        }


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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep internal persistence helpers on the context extension API.")]
        internal async ValueTask<bool> AwaitLegacyReservationAsync(
            Task<bool> reservation,
            CancellationToken cancellationToken)
            => await reservation.WaitAsync(cancellationToken).ConfigureAwait(false);


        internal async Task<DomainEventCollection> SaveCommandEventsDeduplicatedAsync(
            ICommand command,
            DomainEventCollection domainEvents,
            long expectedStreamVersion,
            CancellationToken cancellationToken)
        {
            var envelope = CommandAuditEnvelope.Create(command, context._commandAuditCodec);
            DomainEventCollection? committed = null;
            var accepted = await context._commandDuplicates.Value.TryAcceptAsync(
                command.CommandId,
                envelope.RetrySha256,
                async token =>
                {
                    if (domainEvents.Count == 0)
                    {
                        var reservation = await context._commandAuditWriter.Value
                            .ReserveAsync(envelope, token).ConfigureAwait(false);
                        if (!reservation.Accepted) return false;
                        committed = [];
                        return true;
                    }

                    var preparation = context.AcquireAtomicPreparation(command.StreamId);
                    committed = await context.PrepareAndSaveCommandEventsAtomicallyAsync(
                        preparation, command, domainEvents, expectedStreamVersion, envelope, token)
                        .ConfigureAwait(false);
                    return true;
                },
                cancellationToken).ConfigureAwait(false);
            if (!accepted) throw new CommandAuditDuplicateException(command.CommandId);
            return committed ?? throw new InvalidOperationException("Atomic command persistence completed without an event result.");
        }

        internal async Task<DomainEventCollection> PrepareAndSaveCommandEventsAtomicallyAsync(
            AtomicPreparationLease preparation,
            ICommand command,
            DomainEventCollection domainEvents,
            long expectedStreamVersion,
            CommandAuditEnvelope commandAudit,
            CancellationToken cancellationToken)
        {
            Task<EventLogAppendResult> persistence;
            try
            {
                await preparation.Predecessor.WaitAsync(cancellationToken).ConfigureAwait(false);
                var streamId = await context.GetEventStreamIdAsync(command.StreamId, cancellationToken).ConfigureAwait(false);
                var entries = new List<EventLogAppendEntry>(domainEvents.Count);
                foreach (var domainEvent in domainEvents)
                    entries.Add(new EventLogAppendEntry(
                        await context.GetEventNameIdFromDomainEventAsync(domainEvent, cancellationToken).ConfigureAwait(false),
                        domainEvent));
                persistence = context._atomicCommandEventAppender.Value.AppendAsync(new EventLogAppendRequest(
                    command.StreamId,
                    streamId,
                    command.CommandId,
                    entries,
                    expectedStreamVersion,
                    DateTime.UtcNow,
                    commandAudit), cancellationToken).AsTask();
            }
            finally
            {
                context.ReleaseAtomicPreparation(command.StreamId, preparation);
            }

            var result = await persistence.ConfigureAwait(false);
            return context.ApplyAssignments(domainEvents, result);
        }

        internal async ValueTask<CommandAuditReservation> TryReserveCoreAsync(
            ICommand command,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(command);
            if (context._legacyCommandReservations.TryRemove(command.CommandId, out var legacyReservation))
                return new CommandAuditReservation(
                    await context.AwaitLegacyReservationAsync(legacyReservation.Value, cancellationToken)
                        .ConfigureAwait(false));

            bool accepted;
            if (context._commandAuditOptions.WriteMode == CommandAuditWriteMode.SequentialJsonLegacy)
            {
                accepted = await context._commandDuplicates.Value.TryAcceptAsync(
                        command.CommandId,
                        token => context.InsertCommandLogCoreAsync(
                            command,
                            DateTime.UtcNow,
                            JsonConvert.SerializeObject(command),
                            token),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                // Keep complete audit bytes; opted-in commands compare a separate stable
                // retry identity that excludes only per-attempt tracing metadata.
                var envelope = CommandAuditEnvelope.Create(command, context._commandAuditCodec);
                accepted = await context._commandDuplicates.Value.TryAcceptAsync(
                        command.CommandId,
                        envelope.RetrySha256,
                        async token => (await context._commandAuditWriter.Value
                            .ReserveAsync(envelope, token).ConfigureAwait(false)).Accepted,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            return new CommandAuditReservation(accepted);
        }

        internal async Task ObserveLegacyReservationAsync(
            Guid commandId,
            Lazy<Task<bool>> reservation)
        {
            try
            {
                _ = await reservation.Value.ConfigureAwait(false);
            }
            catch
            {
                context._legacyCommandReservations.TryRemove(
                    new KeyValuePair<Guid, Lazy<Task<bool>>>(commandId, reservation));
                throw;
            }
        }


        internal Task<bool> InsertCommandLogCoreAsync(
            ICommand command,
            DateTime commandTimestamp,
            string commandData,
            CancellationToken cancellationToken)
            => context._dbFactory.ActorEventSourceDb
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

        /// <summary>
        /// Asynchronously retrieves the unique identifier for the specified event stream.
        /// </summary>
        /// <remarks>If the event stream does not exist, it will be created and its identifier will be
        /// returned.</remarks>
        /// <param name="eventStream">The name of the event stream to retrieve the identifier for. Cannot be null or empty.</param>
        /// <returns>A <see langword="long"/> representing the unique identifier of the specified event stream.</returns>
        internal async Task<long> GetEventStreamAsync(string eventStream)
        {
            return (await (context._blackboardService.EventSourcing.EventStreamId.GetAsync(eventStream, InsertEventStream)).ConfigureAwait(false)).EventStreamId;

            async Task<EventStreamIdReadModel> InsertEventStream(string eventStream)
            {
                var eventStreamId = await (context.InsertEventStreamAsync(eventStream)).ConfigureAwait(false);
                return new EventStreamIdReadModel(eventStreamId, eventStream);
            }
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
                    .ExecuteQueryAsync<EventStreamReadModel>(EventSourceActorDbContext.MapToEventStream)).ConfigureAwait(false);

        /// <summary>
        /// Inserts an event stream into the database if it does not already exist.
        /// </summary>
        /// <remarks>The atomic database upsert makes concurrent first use of the same stream safe.</remarks>
        /// <param name="eventStream">The name of the event stream to insert. This value cannot be null or empty.</param>
        /// <returns>The unique identifier of the event stream. If the event stream already exists, its existing identifier is
        /// returned; otherwise, the identifier of the newly inserted event stream is returned.</returns>
        internal async Task<long> InsertEventStreamAsync(string eventStream)
            => await context.InsertEventStreamAsync(eventStream, CancellationToken.None).ConfigureAwait(false);

        internal async Task<long> InsertEventStreamAsync(string eventStream, CancellationToken cancellationToken)
        {
            var eventStreamIdModel = await context._dbFactory.ActorEventSourceDb
                .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventStreamId)}", EventSourceDbSql.GetEventStreamId)
                .SetParameters(new GetEventStreamId(eventStream))
                .ExecuteSingleAsync<EventStreamIdReadModel>(EventSourceActorDbContext.MapToEventStreamId, cancellationToken)
                .ConfigureAwait(false);
            if (eventStreamIdModel is not null)
                return eventStreamIdModel.EventStreamId;

            return await (context._dbFactory.ActorEventSourceDb
                .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.InsertEventStreamId)}", EventSourceDbSql.InsertEventStreamId)
                .SetParameters(new InsertEventStreamId(eventStream))
                .ExecuteScalarAsync(EventSourceActorDbContext.MapToLong, cancellationToken)).ConfigureAwait(false);
        }


        /// <summary>
        /// Asynchronously retrieves the unique identifier for the event name associated with the specified event type.
        /// </summary>
        /// <typeparam name="TEvent">The type of the event, which must implement the <see cref="IEvent"/> interface.</typeparam>
        /// <returns>A task that represents the asynchronous operation. The task result contains the unique identifier  for the event
        /// name associated with the specified event type.</returns>
        internal async Task<int> GetEventNameIdFromTypeAsync<TEvent>() where TEvent : IEvent
            => await context.GetEventNameIdFromTypeAsync<TEvent>(CancellationToken.None).ConfigureAwait(false);

        internal async Task<int> GetEventNameIdFromTypeAsync<TEvent>(CancellationToken cancellationToken) where TEvent : IEvent
            => await context.GetEventNameIdFromTypeAsync(typeof(TEvent), cancellationToken).ConfigureAwait(false);

        /// <summary>
        /// Asynchronously retrieves the unique identifier for an event name based on the specified event type.
        /// </summary>
        /// <remarks>This method interacts with a data source to retrieve or insert the event name identifier. If
        /// the event name  identifier does not exist, it will be created and stored in the database.</remarks>
        /// <param name="eventType">The <see cref="Type"/> of the event for which the identifier is to be retrieved.  The <see
        /// cref="Type.FullName"/> and <see cref="Type.Name"/> properties are used to determine the event name and type.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the unique identifier  for the event
        /// name associated with the specified event type.</returns>
        internal async Task<int> GetEventNameIdFromTypeAsync(Type eventType, CancellationToken cancellationToken)
        {
            var eventTypeFullName = eventType.AssemblyQualifiedName;
            if (!context._eventNameIdCache.TryGetValue(eventTypeFullName, out EventNameIdReadModel eventNameIdModel))
            {
                eventNameIdModel = await context.GetEventNameIdFromDbAsync(eventType.Name, eventTypeFullName, cancellationToken).ConfigureAwait(false);
                if (eventNameIdModel.IsValid)
                {
                    context._eventNameIdCache.TryAdd(eventTypeFullName, eventNameIdModel);
                    return eventNameIdModel.EventNameId;
                }
                var newEventNameIdModel = await InsertEventNameIdAsync(eventType.Name, eventTypeFullName).ConfigureAwait(false);
                context._eventNameIdCache.TryAdd(eventTypeFullName, newEventNameIdModel);
                return newEventNameIdModel.EventNameId;
            }
            return eventNameIdModel.EventNameId;

            async Task<EventNameIdReadModel> InsertEventNameIdAsync(string eventName, string eventTypeName)
            {
                var eventNameIdModel = await context.GetEventNameIdFromDbAsync(eventName, eventTypeName, cancellationToken).ConfigureAwait(false);
                if (eventNameIdModel.IsValid)
                    return eventNameIdModel;
                var eventNameId = await (context._dbFactory.ActorEventSourceDb
                      .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.InsertEventNameId)}", EventSourceDbSql.InsertEventNameId)
                      .SetParameters(new InsertEventNameId(eventName, eventTypeName))
                      .ExecuteScalarAsync(EventSourceActorDbContext.MapToInt, cancellationToken)).ConfigureAwait(false);
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
            => await context.GetEventNameIdFromDbAsync(eventName, eventTypeName, CancellationToken.None).ConfigureAwait(false);

        internal async Task<EventNameIdReadModel> GetEventNameIdFromDbAsync(
            string eventName,
            string eventTypeName,
            CancellationToken cancellationToken)
            => await (context._dbFactory.ActorEventSourceDb
                .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventNameId)}", EventSourceDbSql.GetEventNameId)
                .SetParameters(new GetEventNameId(eventName, eventTypeName))
                .ExecuteSingleAsync<EventNameIdReadModel>(EventSourceActorDbContext.MapToEventNameId, cancellationToken)).ConfigureAwait(false);

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
            var eventLogRange = await (context._dbFactory.ActorEventSourceDb
                .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventLogLastNRange)}", context.EventLogSql(EventSourceDbSql.GetEventLogLastNRange))
                .SetParameters(new GetEventLogLastNRange(eventStreamId))
                .ExecuteQueryAsync<EventStreamReadModel>(EventSourceActorDbContext.MapToEventStream)).ConfigureAwait(false);
            return (eventLogRange is null || eventLogRange.Count == 0)
                ? [] : [.. eventLogRange.Take(lastNRange).OrderBy(e => e.EventVersion)];
        }

    }
}
