using CommandStatus = TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit.CommandStatus;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventProjector.ReadModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Application.Storage.EventSourceDb;

/// <summary>
/// Defines asynchronous actor event-source database commands.
/// </summary>
public interface IEventSourceActorDbWriteContext
{
    Task DeleteEventLogAsync(long eventVersion);
    Task DeleteEventLogsAsync(long[] eventVersions);
    Task DeleteEventLogByStreamIdAsync(long streamId);
    Task DeleteEventStreamByIdAsync(long eventStreamId);
    Task<long> GetEventStreamIdAsync(string eventStream);
    Task<long> GetEventStreamIdAsync(string eventStream, CancellationToken cancellationToken);
    Task<int> GetEventNameIdFromDomainEventAsync<TEvent>(TEvent domainEvent) where TEvent : IEvent;
    Task<int> GetEventNameIdFromDomainEventAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken)
        where TEvent : IEvent;

    Task InsertCommandLogAsync(ICommand command, DateTime commandTimestamp, string commandData);
    Task InsertCommandLogAsync(
        ICommand command,
        DateTime commandTimestamp,
        string commandData,
        CancellationToken cancellationToken);
    Task<bool> TryInsertCommandLogAsync(ICommand command, DateTime commandTimestamp, string commandData);
    Task<bool> TryInsertCommandLogAsync(
        ICommand command,
        DateTime commandTimestamp,
        string commandData,
        CancellationToken cancellationToken);
    Task UpdateCommandLogAsync(Guid commandId, DateTime updateTimestamp, CommandStatus commandStatus);
    Task UpdateCommandLogAsync(
        Guid commandId,
        DateTime updateTimestamp,
        CommandStatus commandStatus,
        CancellationToken cancellationToken);

    Task InsertEventProjectorStateAsync(EventProjectorStateReadModel eventProjectorState);
    Task InsertEventProjectorStateAsync(
        EventProjectorStateReadModel eventProjectorState,
        CancellationToken cancellationToken);
    Task<EventProjectorExecutionStateReadModel?> TryCreateEventProjectorExecutionStateAsync(
        EventProjectorExecutionStateReadModel state,
        CancellationToken cancellationToken = default);
    Task<EventProjectorExecutionStateReadModel?> TryClaimEventProjectorExecutionAsync(
        long eventId,
        string projectorName,
        Guid executionToken,
        DateTime nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);
    Task<EventProjectorExecutionStateReadModel?> TryRenewEventProjectorExecutionAsync(
        long eventId,
        string projectorName,
        Guid executionToken,
        long expectedRevision,
        DateTime nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);
    Task<EventProjectorExecutionStateReadModel?> TryReleaseEventProjectorExecutionAsync(
        EventProjectorStateTransition transition,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);
    Task<EventProjectorExecutionStateReadModel?> TryTransitionEventProjectorExecutionAsync(
        EventProjectorStateTransition transition,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);
    Task<EventProjectorExecutionStateReadModel?> TryTerminalizeEventProjectorExecutionAsync(
        EventProjectorStateTransition transition,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);
    Task<EventProjectorExecutionStateReadModel?> TryTransitionEventProjectorExecutionWithOutboxAsync(
        EventProjectorStateTransition transition,
        EventProjectorOutboxMessage message,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);
    Task<EventProjectorExecutionStateReadModel?> TryTerminalizeEventProjectorExecutionWithOutboxAsync(
        EventProjectorStateTransition transition,
        EventProjectorOutboxMessage message,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EventProjectorOutboxReadModel>> ClaimEventProjectorOutboxAsync(
        string projectorName,
        Guid dispatchToken,
        DateTime nowUtc,
        TimeSpan leaseDuration,
        int batchSize,
        CancellationToken cancellationToken = default);
    Task<bool> MarkEventProjectorOutboxPublishedAsync(
        EventProjectorOutboxReadModel message,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);
    Task<bool> ReleaseEventProjectorOutboxAsync(
        EventProjectorOutboxReadModel message,
        EventProjectorOutboxStatus status,
        DateTime? nextAttemptAtUtc,
        string lastError,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);
    Task<EventProjectorExecutionStateReadModel?> TryRetryEventProjectorExecutionAsync(
        long eventId,
        string projectorName,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);
    Task<EventProjectorExecutionStateReadModel?> TrySkipEventProjectorExecutionAsync(
        long eventId,
        string projectorName,
        string reason,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    Task<DomainEventCollection> SaveEventsAsync(
        string eventStream,
        Guid commandId,
        DomainEventCollection domainEvents);
    Task<DomainEventCollection> SaveEventsAsync(
        string eventStream,
        Guid commandId,
        DomainEventCollection domainEvents,
        CancellationToken cancellationToken);
    Task<DomainEventCollection> SaveEventsAsync(
        string eventStream,
        Guid commandId,
        DomainEventCollection domainEvents,
        long expectedStreamVersion,
        CancellationToken cancellationToken);

    /// <summary>Atomically reserves the uncompressed MessagePack command audit and appends its events.</summary>
    Task<DomainEventCollection> SaveCommandEventsAtomicallyAsync(
        ICommand command,
        DomainEventCollection domainEvents,
        long expectedStreamVersion,
        CancellationToken cancellationToken = default);
}
