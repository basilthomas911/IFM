




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

        /// <summary>Resolves the cached event stream identifier, creating the database entry on a cache miss.</summary>
        /// <param name="eventStream">The event stream name.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>The typed result of the event-source operation.</returns>
        internal async Task<long> ResolveEventStreamIdAsync(string eventStream, CancellationToken cancellationToken)
        {
        var pending = context._blackboardService.EventSourcing.EventStreamId.GetAsync(eventStream, InsertEntityTypeAsync);
        return (await pending.AsTask().WaitAsync(cancellationToken)
            .ConfigureAwait(false)).EventStreamId;

        async Task<EventStreamIdReadModel> InsertEntityTypeAsync(string eventStream)
        {
            var eventStreamId = await context.InsertEventStreamAsync(eventStream, cancellationToken)
            .ConfigureAwait(false);
            return new EventStreamIdReadModel(eventStreamId, eventStream);
        }
        }
        /// <summary>Checks  batch size before the persistence operation.</summary>
        /// <param name="batchSize">The maximum number of records to return or claim.</param>
        internal void ValidateBatchSize(int batchSize)
        {
            if (batchSize is < 1 or > 2_048) throw new ArgumentOutOfRangeException(nameof(batchSize));
        }
        /// <summary>Checks  release transition before the persistence operation.</summary>
        /// <param name="transition">The requested revision and lease-qualified projector transition.</param>
        internal void ValidateReleaseTransition(EventProjectorStateTransition transition)
        {
            context.ValidateTransition(transition, terminal: false);
            if (transition.Outcome != EventProjectorOutcomeType.Retrying)
                throw new ArgumentOutOfRangeException(nameof(transition), "A released execution must be retryable.");
            if (!transition.NextAttemptAtUtc.HasValue || !transition.LastErrorAtUtc.HasValue)
                throw new ArgumentException("A released execution requires retry and error timestamps.", nameof(transition));
        }
        /// <summary>Checks  outbox release status before the persistence operation.</summary>
        /// <param name="status">The requested projector or outbox status.</param>
        internal void ValidateOutboxReleaseStatus(EventProjectorOutboxStatus status)
        {
            if (status is not EventProjectorOutboxStatus.Retrying and not EventProjectorOutboxStatus.Failed)
                throw new ArgumentOutOfRangeException(nameof(status));
        }
        /// <summary>Checks  operational status before the persistence operation.</summary>
        /// <param name="status">The requested projector or outbox status.</param>
        internal void ValidateOperationalStatus(EventProjectorOperationalStatus status)
        {
            if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        }
        /// <summary>Checks  skip reason before the persistence operation.</summary>
        /// <param name="reason">The operator supplied reason for skipping the event.</param>
        internal void ValidateSkipReason(string reason)
        {
            context.ValidateTextArgument(reason, nameof(reason));
            if (reason.Length > 2_000) throw new ArgumentOutOfRangeException(nameof(reason));
        }
        /// <summary>Checks  qualification writer before the persistence operation.</summary>
        /// <param name="options">The event log persistence configuration.</param>
        internal void ValidateQualificationWriter(EventLogPersistenceOptions options)
        {
            if (options.WriteMode != EventLogWriteMode.BinaryCopy)
                throw new ArgumentException("Actor qualification requires the binary-copy writer.", nameof(options));
        }

        /// <summary>Checks  required argument before the persistence operation.</summary>
        /// <param name="argument">The argument used by this operation.</param>
        /// <param name="parameterName">The parameter name used by this operation.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Argument validation belongs to the context extension API.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3236", Justification = "Forward the original operation parameter name rather than the helper parameter name.")]
        internal void ValidateRequiredArgument([System.Diagnostics.CodeAnalysis.NotNull] object? argument, string parameterName)
            => ArgumentNullException.ThrowIfNull(argument, parameterName);

        /// <summary>Checks  text argument before the persistence operation.</summary>
        /// <param name="argument">The argument used by this operation.</param>
        /// <param name="parameterName">The parameter name used by this operation.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Argument validation belongs to the context extension API.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3236", Justification = "Forward the original operation parameter name rather than the helper parameter name.")]
        internal void ValidateTextArgument([System.Diagnostics.CodeAnalysis.NotNull] string? argument, string parameterName)
            => ArgumentException.ThrowIfNullOrWhiteSpace(argument, parameterName);

        /// <summary>Checks  nonnegative argument before the persistence operation.</summary>
        /// <param name="argument">The argument used by this operation.</param>
        /// <param name="parameterName">The parameter name used by this operation.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Argument validation belongs to the context extension API.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3236", Justification = "Forward the original operation parameter name rather than the helper parameter name.")]
        internal void ValidateNonnegativeArgument(long argument, string parameterName)
            => ArgumentOutOfRangeException.ThrowIfLessThan(argument, 0, parameterName);

        /// <summary>Checks  positive argument before the persistence operation.</summary>
        /// <param name="argument">The argument used by this operation.</param>
        /// <param name="parameterName">The parameter name used by this operation.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Argument validation belongs to the context extension API.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3236", Justification = "Forward the original operation parameter name rather than the helper parameter name.")]
        internal void ValidatePositiveArgument(long argument, string parameterName)
            => ArgumentOutOfRangeException.ThrowIfNegativeOrZero(argument, parameterName);

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

        /// <summary>Checks  projector identity before the persistence operation.</summary>
        /// <param name="eventId">The source event identifier.</param>
        /// <param name="projectorName">The projector identity.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Validation belongs to the context extension API.")]
        internal void ValidateProjectorIdentity(long eventId, string projectorName)
        {
            if (eventId <= 0)
                throw new ArgumentOutOfRangeException(nameof(eventId), eventId, "The event id must be positive.");
            ArgumentException.ThrowIfNullOrWhiteSpace(projectorName);
        }

        /// <summary>Checks  execution token before the persistence operation.</summary>
        /// <param name="executionToken">The token identifying the current execution lease owner.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Validation belongs to the context extension API.")]
        internal void ValidateExecutionToken(Guid executionToken)
        {
            if (executionToken == Guid.Empty)
                throw new ArgumentException("The execution token cannot be empty.", nameof(executionToken));
        }

        /// <summary>Require Utc using the event-source persistence services.</summary>
        /// <param name="value">The value used by this operation.</param>
        /// <param name="parameterName">The parameter name used by this operation.</param>
        /// <returns>The typed result of the event-source operation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Validation belongs to the context extension API.")]
        internal DateTime RequireUtc(DateTime value, string parameterName)
        {
            if (value.Kind != DateTimeKind.Utc)
                throw new ArgumentException("The timestamp must use DateTimeKind.Utc.", parameterName);
            return value;
        }

        /// <summary>Checks  optional utc before the persistence operation.</summary>
        /// <param name="value">The value used by this operation.</param>
        /// <param name="parameterName">The parameter name used by this operation.</param>
        /// <returns>The typed result of the event-source operation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Validation belongs to the context extension API.")]
        internal DateTime? ValidateOptionalUtc(DateTime? value, string parameterName)
            => value.HasValue ? context.RequireUtc(value.Value, parameterName) : null;

        /// <summary>Get Lease Expiry using the event-source persistence services.</summary>
        /// <param name="nowUtc">The current UTC timestamp.</param>
        /// <param name="leaseDuration">The requested lease duration.</param>
        /// <returns>The typed result of the event-source operation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Validation belongs to the context extension API.")]
        internal DateTime GetLeaseExpiry(DateTime nowUtc, TimeSpan leaseDuration)
        {
            context.RequireUtc(nowUtc, nameof(nowUtc));
            if (leaseDuration <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(leaseDuration), leaseDuration, "The lease duration must be positive.");
            return nowUtc.Add(leaseDuration);
        }

        /// <summary>Checks  transition before the persistence operation.</summary>
        /// <param name="transition">The requested revision and lease-qualified projector transition.</param>
        /// <param name="terminal">The terminal used by this operation.</param>
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

        /// <summary>Checks  outbox message before the persistence operation.</summary>
        /// <param name="transition">The requested revision and lease-qualified projector transition.</param>
        /// <param name="message">The persisted outbox message and its dispatch ownership.</param>
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


        /// <summary>Save Command Events Deduplicated using the event-source persistence services.</summary>
        /// <param name="command">The command being audited or committed.</param>
        /// <param name="domainEvents">The ordered events to persist.</param>
        /// <param name="expectedStreamVersion">The committed stream version required for optimistic concurrency.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>The typed result of the event-source operation.</returns>
        internal async Task<DomainEventCollection> SaveCommandEventsCoreAsync(
            ICommand command,
            DomainEventCollection domainEvents,
            long expectedStreamVersion,
            CancellationToken cancellationToken)
        {
            var envelope = CommandAuditEnvelope.Create(command, context._commandAuditCodec);
            if (domainEvents.Count == 0)
            {
                if (!await context.TryInsertCommandAuditAsync(envelope, cancellationToken).ConfigureAwait(false))
                    throw new CommandAuditDuplicateException(command.CommandId);
                return [];
            }
            var preparation = context.AcquireAtomicPreparation(command.StreamId);
            return await context.PrepareAndSaveCommandEventsAtomicallyAsync(
                preparation, command, domainEvents, expectedStreamVersion, envelope, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Prepare And Save Command Events Atomically using the event-source persistence services.</summary>
        /// <param name="preparation">The preparation used by this operation.</param>
        /// <param name="command">The command being audited or committed.</param>
        /// <param name="domainEvents">The ordered events to persist.</param>
        /// <param name="expectedStreamVersion">The committed stream version required for optimistic concurrency.</param>
        /// <param name="commandAudit">The command audit used by this operation.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>The typed result of the event-source operation.</returns>
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
                await preparation.Predecessor.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
                var streamId = await context.GetEventStreamIdAsync(command.StreamId, cancellationToken)
            .ConfigureAwait(false);
                var entries = new List<EventLogAppendEntry>(domainEvents.Count);
                foreach (var domainEvent in domainEvents)
                    entries.Add(new EventLogAppendEntry(
                        await context.GetEventNameIdFromDomainEventAsync(domainEvent, cancellationToken)
            .ConfigureAwait(false),
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
            return (await (context._blackboardService.EventSourcing.EventStreamId.GetAsync(eventStream, InsertEventStream))
            .ConfigureAwait(false)).EventStreamId;

            async Task<EventStreamIdReadModel> InsertEventStream(string eventStream)
            {
                var eventStreamId = await (context.InsertEventStreamAsync(eventStream))
            .ConfigureAwait(false);
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
                var eventNameId = await (context._dbFactory.ActorEventSourceDb
                      .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.InsertEventNameId)}", EventSourceDbSql.InsertEventNameId)
                      .SetParameters(new InsertEventNameId(eventName, eventTypeName))
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
            var eventLogRange = await (context._dbFactory.ActorEventSourceDb
                .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.GetEventLogLastNRange)}", context.EventLogSql(EventSourceDbSql.GetEventLogLastNRange))
                .SetParameters(new GetEventLogLastNRange(eventStreamId))
                .ExecuteQueryAsync<EventStreamReadModel>(EventSourceActorDbContext.MapToEventStream))
            .ConfigureAwait(false);
            return (eventLogRange is null || eventLogRange.Count == 0)
                ? [] : [.. eventLogRange.Take(lastNRange).OrderBy(e => e.EventVersion)];
        }

    }
}
