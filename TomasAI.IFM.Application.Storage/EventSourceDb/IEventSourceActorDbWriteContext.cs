namespace TomasAI.IFM.Application.Storage.EventSourceDb
{
    using CommandStatus = TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit.CommandStatus;
    using TomasAI.IFM.Shared.EventProjector;
    using TomasAI.IFM.Shared.EventProjector.ReadModels;
    using TomasAI.IFM.Shared.EventSourcing;

    /// <summary>
    /// Defines asynchronous actor event-source database commands.
    /// </summary>
    public interface IEventSourceActorDbWriteContext
    {
        /// <summary>Atomically indexes a full-state sidecar checkpoint at the authoritative source revision.</summary>
        /// <param name="stream">The exact event stream name.</param>
        /// <param name="sidecar">The sidecar supplied by the caller.</param>
        /// <param name="sourceRevision">The source revision supplied by the caller.</param>
        /// <param name="snapshot">The snapshot supplied by the caller.</param>
        /// <param name="cancellationToken">Cancels the database operation.</param>
        /// <returns>True when the requested condition holds.</returns>
        Task<bool> SaveAggregateCheckpointAsync(string stream, string sidecar, long sourceRevision, IEvent snapshot, CancellationToken cancellationToken);
        /// <summary>Appends a source transition and full-state checkpoint under one optimistic source fence.</summary>
        /// <param name="stream">The exact event stream name.</param>
        /// <param name="expectedRevision">The expected revision supplied by the caller.</param>
        /// <param name="source">The source supplied by the caller.</param>
        /// <param name="checkpoint">The checkpoint supplied by the caller.</param>
        /// <param name="cancellationToken">Cancels the database operation.</param>
        /// <returns>True when the requested condition holds.</returns>
        Task<bool> SaveAggregateSourceWithCheckpointAsync(string stream, long expectedRevision, IEvent source, IEvent checkpoint, CancellationToken cancellationToken);

        /// <summary>Attempts a durable insertion of supplied command audit facts.</summary>
        /// <param name="envelope">The encoded command audit facts.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Whether this request inserted the command audit.</returns>
        ValueTask<bool> TryInsertCommandAuditAsync(TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit.CommandAuditEnvelope envelope, CancellationToken cancellationToken = default);

        /// <summary>Delete Event Log using the event-source persistence services.</summary>
        /// <param name="eventVersion">The source event version.</param>
        /// <returns>The completion of the persistence operation.</returns>
        Task DeleteEventLogAsync(long eventVersion);
        /// <summary>Delete Event Logs using the event-source persistence services.</summary>
        /// <param name="eventVersions">The event versions used by this operation.</param>
        /// <returns>The completion of the persistence operation.</returns>
        Task DeleteEventLogsAsync(long[] eventVersions);
        /// <summary>Delete Event Log By Stream Id using the event-source persistence services.</summary>
        /// <param name="streamId">The event stream identifier.</param>
        /// <returns>The completion of the persistence operation.</returns>
        Task DeleteEventLogByStreamIdAsync(long streamId);
        /// <summary>Delete Event Stream By Id using the event-source persistence services.</summary>
        /// <param name="eventStreamId">The event stream identifier.</param>
        /// <returns>The completion of the persistence operation.</returns>
        Task DeleteEventStreamByIdAsync(long eventStreamId);
        /// <summary>Get Event Stream Id using the event-source persistence services.</summary>
        /// <param name="eventStream">The event stream name.</param>
        /// <returns>The typed result of the event-source operation.</returns>
        Task<long> GetEventStreamIdAsync(string eventStream);
        /// <summary>Get Event Stream Id using the event-source persistence services.</summary>
        /// <param name="eventStream">The event stream name.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>The typed result of the event-source operation.</returns>
        Task<long> GetEventStreamIdAsync(string eventStream, CancellationToken cancellationToken);
        /// <summary>Get Event Name Id From Domain Event using the event-source persistence services.</summary>
        /// <param name="domainEvent">The domain event used by this operation.</param>
        /// <typeparam name="TEvent">The event, snapshot, state or record type used by the operation.</typeparam>
        /// <returns>The typed result of the event-source operation.</returns>
        Task<int> GetEventNameIdFromDomainEventAsync<TEvent>(TEvent domainEvent) where TEvent : IEvent;
        /// <summary>Get Event Name Id From Domain Event using the event-source persistence services.</summary>
        /// <param name="domainEvent">The domain event used by this operation.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <typeparam name="TEvent">The event, snapshot, state or record type used by the operation.</typeparam>
        /// <returns>The typed result of the event-source operation.</returns>
        Task<int> GetEventNameIdFromDomainEventAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken)
            where TEvent : IEvent;

        /// <summary>Insert Command Log using the event-source persistence services.</summary>
        /// <param name="command">The command being audited or committed.</param>
        /// <param name="commandTimestamp">The command audit timestamp.</param>
        /// <param name="commandData">The serialized command audit payload.</param>
        /// <returns>The completion of the persistence operation.</returns>
        Task InsertCommandLogAsync(ICommand command, DateTime commandTimestamp, string commandData);
        /// <summary>Insert Command Log using the event-source persistence services.</summary>
        /// <param name="command">The command being audited or committed.</param>
        /// <param name="commandTimestamp">The command audit timestamp.</param>
        /// <param name="commandData">The serialized command audit payload.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>The completion of the persistence operation.</returns>
        Task InsertCommandLogAsync(
            ICommand command,
            DateTime commandTimestamp,
            string commandData,
            CancellationToken cancellationToken);
        /// <summary>Try Insert Command Log using the event-source persistence services.</summary>
        /// <param name="command">The command being audited or committed.</param>
        /// <param name="commandTimestamp">The command audit timestamp.</param>
        /// <param name="commandData">The serialized command audit payload.</param>
        /// <returns>The typed result of the event-source operation.</returns>
        Task<bool> TryInsertCommandLogAsync(ICommand command, DateTime commandTimestamp, string commandData);
        /// <summary>Try Insert Command Log using the event-source persistence services.</summary>
        /// <param name="command">The command being audited or committed.</param>
        /// <param name="commandTimestamp">The command audit timestamp.</param>
        /// <param name="commandData">The serialized command audit payload.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>The typed result of the event-source operation.</returns>
        Task<bool> TryInsertCommandLogAsync(
            ICommand command,
            DateTime commandTimestamp,
            string commandData,
            CancellationToken cancellationToken);
        /// <summary>Update Command Log using the event-source persistence services.</summary>
        /// <param name="commandId">The originating command identifier.</param>
        /// <param name="updateTimestamp">The command status update timestamp.</param>
        /// <param name="commandStatus">The new command audit status.</param>
        /// <returns>The completion of the persistence operation.</returns>
        Task UpdateCommandLogAsync(Guid commandId, DateTime updateTimestamp, CommandStatus commandStatus);
        /// <summary>Update Command Log using the event-source persistence services.</summary>
        /// <param name="commandId">The originating command identifier.</param>
        /// <param name="updateTimestamp">The command status update timestamp.</param>
        /// <param name="commandStatus">The new command audit status.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>The completion of the persistence operation.</returns>
        Task UpdateCommandLogAsync(
            Guid commandId,
            DateTime updateTimestamp,
            CommandStatus commandStatus,
            CancellationToken cancellationToken);

        /// <summary>Insert Event Projector State using the event-source persistence services.</summary>
        /// <param name="eventProjectorState">The event projector state used by this operation.</param>
        /// <returns>The completion of the persistence operation.</returns>
        Task InsertEventProjectorStateAsync(EventProjectorStateReadModel eventProjectorState);
        /// <summary>Insert Event Projector State using the event-source persistence services.</summary>
        /// <param name="eventProjectorState">The event projector state used by this operation.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>The completion of the persistence operation.</returns>
        Task InsertEventProjectorStateAsync(
            EventProjectorStateReadModel eventProjectorState,
            CancellationToken cancellationToken);
        /// <summary>Try Create Event Projector Execution State using the event-source persistence services.</summary>
        /// <param name="state">The projector state to persist.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>The typed result of the event-source operation.</returns>
        Task<EventProjectorExecutionStateReadModel?> TryCreateEventProjectorExecutionStateAsync(
            EventProjectorExecutionStateReadModel state,
            CancellationToken cancellationToken = default);
        /// <summary>Try Claim Event Projector Execution using the event-source persistence services.</summary>
        /// <param name="lease">The execution lease prepared by the caller.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>The typed result of the event-source operation.</returns>
        Task<EventProjectorExecutionStateReadModel?> TryClaimEventProjectorExecutionAsync(
            EventProjectorLease lease,
            CancellationToken cancellationToken = default);
        /// <summary>Try Renew Event Projector Execution using the event-source persistence services.</summary>
        /// <param name="lease">The execution lease prepared by the caller.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>The typed result of the event-source operation.</returns>
        Task<EventProjectorExecutionStateReadModel?> TryRenewEventProjectorExecutionAsync(
            EventProjectorLease lease,
            CancellationToken cancellationToken = default);
        /// <summary>Try Release Event Projector Execution using the event-source persistence services.</summary>
        /// <param name="transition">The requested revision and lease-qualified projector transition.</param>
        /// <param name="nowUtc">The current UTC timestamp.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>The typed result of the event-source operation.</returns>
        Task<EventProjectorExecutionStateReadModel?> TryReleaseEventProjectorExecutionAsync(
            EventProjectorStateTransition transition,
            DateTime nowUtc,
            CancellationToken cancellationToken = default);
        /// <summary>Try Transition Event Projector Execution using the event-source persistence services.</summary>
        /// <param name="transition">The requested revision and lease-qualified projector transition.</param>
        /// <param name="nowUtc">The current UTC timestamp.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>The typed result of the event-source operation.</returns>
        Task<EventProjectorExecutionStateReadModel?> TryTransitionEventProjectorExecutionAsync(
            EventProjectorStateTransition transition,
            DateTime nowUtc,
            CancellationToken cancellationToken = default);
        /// <summary>Try Terminalize Event Projector Execution using the event-source persistence services.</summary>
        /// <param name="transition">The requested revision and lease-qualified projector transition.</param>
        /// <param name="nowUtc">The current UTC timestamp.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>The typed result of the event-source operation.</returns>
        Task<EventProjectorExecutionStateReadModel?> TryTerminalizeEventProjectorExecutionAsync(
            EventProjectorStateTransition transition,
            DateTime nowUtc,
            CancellationToken cancellationToken = default);
        /// <summary>Try Transition Event Projector Execution With Outbox using the event-source persistence services.</summary>
        /// <param name="transition">The requested revision and lease-qualified projector transition.</param>
        /// <param name="message">The persisted outbox message and its dispatch ownership.</param>
        /// <param name="nowUtc">The current UTC timestamp.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>The typed result of the event-source operation.</returns>
        Task<EventProjectorExecutionStateReadModel?> TryTransitionEventProjectorExecutionWithOutboxAsync(
            EventProjectorStateTransition transition,
            EventProjectorOutboxMessage message,
            DateTime nowUtc,
            CancellationToken cancellationToken = default);
        /// <summary>Try Terminalize Event Projector Execution With Outbox using the event-source persistence services.</summary>
        /// <param name="transition">The requested revision and lease-qualified projector transition.</param>
        /// <param name="message">The persisted outbox message and its dispatch ownership.</param>
        /// <param name="nowUtc">The current UTC timestamp.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>The typed result of the event-source operation.</returns>
        Task<EventProjectorExecutionStateReadModel?> TryTerminalizeEventProjectorExecutionWithOutboxAsync(
            EventProjectorStateTransition transition,
            EventProjectorOutboxMessage message,
            DateTime nowUtc,
            CancellationToken cancellationToken = default);
        /// <summary>Claim Event Projector Outbox using the event-source persistence services.</summary>
        /// <param name="projectorName">The projector identity.</param>
        /// <param name="dispatchToken">The token identifying the current outbox lease owner.</param>
        /// <param name="nowUtc">The current UTC timestamp.</param>
        /// <param name="leaseDuration">The requested lease duration.</param>
        /// <param name="batchSize">The maximum number of records to return or claim.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>The typed result of the event-source operation.</returns>
        Task<IReadOnlyList<EventProjectorOutboxReadModel>> ClaimEventProjectorOutboxAsync(
            string projectorName,
            Guid dispatchToken,
            DateTime nowUtc,
            TimeSpan leaseDuration,
            int batchSize,
            CancellationToken cancellationToken = default);
        /// <summary>Mark Event Projector Outbox Published using the event-source persistence services.</summary>
        /// <param name="message">The persisted outbox message and its dispatch ownership.</param>
        /// <param name="nowUtc">The current UTC timestamp.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>The typed result of the event-source operation.</returns>
        Task<bool> MarkEventProjectorOutboxPublishedAsync(
            EventProjectorOutboxReadModel message,
            DateTime nowUtc,
            CancellationToken cancellationToken = default);
        /// <summary>Release Event Projector Outbox using the event-source persistence services.</summary>
        /// <param name="message">The persisted outbox message and its dispatch ownership.</param>
        /// <param name="status">The requested projector or outbox status.</param>
        /// <param name="nextAttemptAtUtc">The next eligible retry time in UTC.</param>
        /// <param name="lastError">The diagnostic error from the failed attempt.</param>
        /// <param name="nowUtc">The current UTC timestamp.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>The typed result of the event-source operation.</returns>
        Task<bool> ReleaseEventProjectorOutboxAsync(
            EventProjectorOutboxReadModel message,
            EventProjectorOutboxStatus status,
            DateTime? nextAttemptAtUtc,
            string lastError,
            DateTime nowUtc,
            CancellationToken cancellationToken = default);
        /// <summary>Try Retry Event Projector Execution using the event-source persistence services.</summary>
        /// <param name="eventId">The source event identifier.</param>
        /// <param name="projectorName">The projector identity.</param>
        /// <param name="nowUtc">The current UTC timestamp.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>The typed result of the event-source operation.</returns>
        Task<EventProjectorExecutionStateReadModel?> TryRetryEventProjectorExecutionAsync(
            long eventId,
            string projectorName,
            DateTime nowUtc,
            CancellationToken cancellationToken = default);
        /// <summary>Try Skip Event Projector Execution using the event-source persistence services.</summary>
        /// <param name="eventId">The source event identifier.</param>
        /// <param name="projectorName">The projector identity.</param>
        /// <param name="reason">The operator supplied reason for skipping the event.</param>
        /// <param name="nowUtc">The current UTC timestamp.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>The typed result of the event-source operation.</returns>
        Task<EventProjectorExecutionStateReadModel?> TrySkipEventProjectorExecutionAsync(
            long eventId,
            string projectorName,
            string reason,
            DateTime nowUtc,
            CancellationToken cancellationToken = default);

        /// <summary>Appends ordered domain events and assigns their committed event and stream versions.</summary>
        /// <param name="eventStream">The event stream name.</param>
        /// <param name="commandId">The originating command identifier.</param>
        /// <param name="domainEvents">The ordered events to persist.</param>
        /// <returns>The typed result of the event-source operation.</returns>
        Task<DomainEventCollection> SaveEventsAsync(
            string eventStream,
            Guid commandId,
            DomainEventCollection domainEvents);
        /// <summary>Appends ordered domain events and assigns their committed event and stream versions.</summary>
        /// <param name="eventStream">The event stream name.</param>
        /// <param name="commandId">The originating command identifier.</param>
        /// <param name="domainEvents">The ordered events to persist.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>The typed result of the event-source operation.</returns>
        Task<DomainEventCollection> SaveEventsAsync(
            string eventStream,
            Guid commandId,
            DomainEventCollection domainEvents,
            CancellationToken cancellationToken);
        /// <summary>Appends ordered domain events and assigns their committed event and stream versions.</summary>
        /// <param name="eventStream">The event stream name.</param>
        /// <param name="commandId">The originating command identifier.</param>
        /// <param name="domainEvents">The ordered events to persist.</param>
        /// <param name="expectedStreamVersion">The committed stream version required for optimistic concurrency.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>The typed result of the event-source operation.</returns>
        Task<DomainEventCollection> SaveEventsAsync(
            string eventStream,
            Guid commandId,
            DomainEventCollection domainEvents,
            long expectedStreamVersion,
            CancellationToken cancellationToken);

        /// <summary>Atomically reserves the uncompressed MessagePack command audit and appends its events.</summary>
        /// <param name="audit">The prepared command audit envelope.</param>
        /// <param name="domainEvents">The ordered events to persist.</param>
        /// <param name="expectedStreamVersion">The committed stream version required for optimistic concurrency.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>The typed result of the event-source operation.</returns>
        Task<DomainEventCollection> PersistCommandEventBatchAsync(
            TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit.CommandAuditEnvelope audit,
            DomainEventCollection domainEvents,
            long expectedStreamVersion,
            CancellationToken cancellationToken = default);
    }

    /// <summary>Request-scoped operations on an EventSource-owned PostgreSQL transaction.</summary>
    public interface IEnlistedPostgresTransaction
    {
        Task<int> ExecuteAsync(string sql, object?[] parameters, CancellationToken cancellationToken, PostgresQueryTimeout? timeout = null);
        Task<object?> ScalarAsync(string sql, object?[] parameters, CancellationToken cancellationToken, PostgresQueryTimeout? timeout = null);
        Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object?[] parameters,
            Func<System.Data.Common.DbDataReader, T> map, CancellationToken cancellationToken, PostgresQueryTimeout? timeout = null);
    }

    /// <summary>Runs business writes and the existing event-store append on one connection/transaction.</summary>
    public interface IPostgresEventTransaction
    {
        /// <summary>Executes once without replaying the delegate after a confirmed rollback.</summary>
        Task<T> ExecuteOnceAsync<T>(Func<EnlistedEventTransaction, CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken = default);

        Task<T> ExecuteAsync<T>(Func<EnlistedEventTransaction, CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken = default);
    }
}

namespace TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit
{
    internal interface ICommandAuditWriter : IAsyncDisposable
    {
        ValueTask<CommandAuditWriteResult> ReserveAsync(
            CommandAuditEnvelope envelope,
            CancellationToken cancellationToken = default);
    }
}

namespace TomasAI.IFM.Application.Storage.EventSourceDb.EventLog
{
    public interface IEventLogAppender : IAsyncDisposable
    {
        EventLogWriteMode WriteMode { get; }
        bool UseLz4Compression { get; }
        ValueTask<EventLogAppendResult> AppendAsync(EventLogAppendRequest request, CancellationToken cancellationToken = default);
    }
}
