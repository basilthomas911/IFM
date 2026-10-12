using CommandStatus = TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit.CommandStatus;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventProjector.ReadModels;
using TomasAI.IFM.Shared.EventSourcing;


namespace TomasAI.IFM.Application.Storage.EventSourceDb;

/// <summary>
/// Defines asynchronous actor event-source database commands.
/// </summary>
public interface IEventSourceActorDbWriteContext
{
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
    /// <param name="eventId">The source event identifier.</param>
    /// <param name="projectorName">The projector identity.</param>
    /// <param name="executionToken">The token identifying the current execution lease owner.</param>
    /// <param name="nowUtc">The current UTC timestamp.</param>
    /// <param name="leaseDuration">The requested lease duration.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    Task<EventProjectorExecutionStateReadModel?> TryClaimEventProjectorExecutionAsync(
        long eventId,
        string projectorName,
        Guid executionToken,
        DateTime nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);
    /// <summary>Try Renew Event Projector Execution using the event-source persistence services.</summary>
    /// <param name="eventId">The source event identifier.</param>
    /// <param name="projectorName">The projector identity.</param>
    /// <param name="executionToken">The token identifying the current execution lease owner.</param>
    /// <param name="expectedRevision">The expected revision used by this operation.</param>
    /// <param name="nowUtc">The current UTC timestamp.</param>
    /// <param name="leaseDuration">The requested lease duration.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    Task<EventProjectorExecutionStateReadModel?> TryRenewEventProjectorExecutionAsync(
        long eventId,
        string projectorName,
        Guid executionToken,
        long expectedRevision,
        DateTime nowUtc,
        TimeSpan leaseDuration,
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
    /// <param name="command">The command being audited or committed.</param>
    /// <param name="domainEvents">The ordered events to persist.</param>
    /// <param name="expectedStreamVersion">The committed stream version required for optimistic concurrency.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    Task<DomainEventCollection> SaveCommandEventsAtomicallyAsync(
        ICommand command,
        DomainEventCollection domainEvents,
        long expectedStreamVersion,
        CancellationToken cancellationToken = default);
}
