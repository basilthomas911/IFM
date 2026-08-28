using StackExchange.Redis;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventProjector.ReadModels;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Application.Storage;

/// <summary>
/// Defines a contract for interacting with the event sourcing database for actor state management.
/// </summary>
/// <remarks>This interface provides methods for managing event streams, including saving, deleting, and loading
/// events, as well as performing map-reduce operations on event streams. It is designed to support event-sourced
/// systems where actor states are reconstructed from event streams.</remarks>
public interface IEventSourceActorDbContext
{
    Task DeleteEventLogAsync(long eventVersion);
    Task DeleteEventLogsAsync(long[] eventVersions);
    Task DeleteEventLogByStreamIdAsync(long streamId);
    Task DeleteEventStreamByIdAsync(long eventStreamId);
    Task<long> GetEventStreamIdAsync(string eventStream);
    Task<long> GetEventStreamIdAsync(string eventStream, CancellationToken cancellationToken);
    Task<EventStreamIdReadModel?> GetEventStreamIdFromDbAsync(string eventStream);
    Task<int> GetEventNameIdFromDomainEventAsync<TEvent>(TEvent domainEvent) where TEvent : IEvent;
    Task<int> GetEventNameIdFromDomainEventAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken) where TEvent : IEvent;
    Task InsertCommandLogAsync(ICommand command, DateTime commandTimestamp, string commandData);
    Task InsertCommandLogAsync(ICommand command, DateTime commandTimestamp, string commandData, CancellationToken cancellationToken);
    Task<bool> TryInsertCommandLogAsync(ICommand command, DateTime commandTimestamp, string commandData);
    Task<bool> TryInsertCommandLogAsync(ICommand command, DateTime commandTimestamp, string commandData, CancellationToken cancellationToken);
    Task<CommandLogReadModel?> GetCommandLogAsync(Guid commandId);
    Task<bool> HasEventForCommandAsync(Guid commandId);
    Task UpdateCommandLogAsync(Guid commandId, DateTime updateTimestamp, CommandStatus commandStatus);
    Task UpdateCommandLogAsync(Guid commandId, DateTime updateTimestamp, CommandStatus commandStatus, CancellationToken cancellationToken);

    Task InsertEventProjectorStateAsync(EventProjectorStateReadModel eventProjectorState);
    Task InsertEventProjectorStateAsync(
        EventProjectorStateReadModel eventProjectorState,
        CancellationToken cancellationToken);
    Task<EventProjectorStateReadModel?> GetEventProjectorStateAsync(long eventId, string projectorName);
    Task<EventProjectorStateReadModel?> GetEventProjectorStateAsync(
        long eventId,
        string projectorName,
        CancellationToken cancellationToken);
    Task<EventLogReadModel?> GetEventLogByEventIdAsync(
        long eventId,
        CancellationToken cancellationToken = default);
    Task<EventProjectorExecutionStateReadModel?> TryCreateEventProjectorExecutionStateAsync(
        EventProjectorExecutionStateReadModel state,
        CancellationToken cancellationToken = default);
    Task<EventProjectorExecutionStateReadModel?> GetEventProjectorExecutionStateAsync(
        long eventId,
        string projectorName,
        CancellationToken cancellationToken = default);
    Task<EventProjectorStreamCheckpointReadModel?> GetEventProjectorStreamCheckpointAsync(
        string projectorName,
        long eventStreamId,
        CancellationToken cancellationToken = default);
    Task<EventProjectorExecutionStateReadModel?> TryClaimEventProjectorExecutionAsync(
        long eventId,
        string projectorName,
        Guid executionToken,
        DateTime nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);
    Task<bool> HasEarlierUnresolvedEventProjectorExecutionAsync(
        long eventId,
        string projectorName,
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
    Task<IReadOnlyList<EventProjectorExecutionStateReadModel>> GetEventProjectorOperationalStatePageAsync(
        string projectorName,
        EventProjectorOperationalStatus status,
        long afterEventId,
        int batchSize,
        CancellationToken cancellationToken = default);
    Task<EventProjectorOperationalSnapshotReadModel> GetEventProjectorOperationalSnapshotAsync(
        string projectorName,
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
    Task<IReadOnlyList<EventProjectorRecoveryItemReadModel>> GetEventProjectorRecoveryPageAsync(
        string projectorName,
        IReadOnlyCollection<string> eventNames,
        long afterEventId,
        DateTime nowUtc,
        int batchSize,
        CancellationToken cancellationToken = default);
    Task<ICollection<EventLogReadModel>> GetUncompletedEventProjectorEventsAsync(
        string projectorName,
        IReadOnlyCollection<string> eventNames);
    Task<ICollection<EventLogReadModel>> GetUncompletedEventProjectorEventsAsync(
        string projectorName,
        IReadOnlyCollection<string> eventNames,
        CancellationToken cancellationToken);

    Task<DomainEventCollection> SaveEventsAsync( string eventStream, Guid commandId, DomainEventCollection domainEvents);
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

    ValueTask MapReduceActorEventStreamAsync<TState>(long eventStreamId, Action<IEnumerable<EventStreamReadModel>> reducerAction)
    where TState : IActorState<TState>;
    ValueTask MapReduceActorEventStreamAsync<TState>(
        long eventStreamId,
        Action<IEnumerable<EventStreamReadModel>> reducerAction,
        CancellationToken cancellationToken)
    where TState : IActorState<TState>;

    ValueTask MapReduceActorEventStreamAsync<TState, TEvent>(long eventStreamId, int lastNRange, Action<IEnumerable<EventStreamReadModel>> reducerAction)
        where TState : IActorState<TState> where TEvent : IEvent;
    ValueTask MapReduceActorEventStreamAsync<TState, TEvent>(
        long eventStreamId,
        int lastNRange,
        Action<IEnumerable<EventStreamReadModel>> reducerAction,
        CancellationToken cancellationToken)
        where TState : IActorState<TState> where TEvent : IEvent;

    ValueTask MapReduceActorEventStreamAsync<TState, TSnapshot>(long eventStreamId, Action<IEnumerable<EventStreamReadModel>> reducerAction)
        where TState : IActorState<TState> where TSnapshot : IEvent;
    ValueTask MapReduceActorEventStreamAsync<TState, TSnapshot>(
        long eventStreamId,
        Action<IEnumerable<EventStreamReadModel>> reducerAction,
        CancellationToken cancellationToken)
        where TState : IActorState<TState> where TSnapshot : IEvent;

    ValueTask MapReduceActorEventStreamFromSnapshotLastNRangeAsync<TState, TSnapshot, TRangeEvent>(
        long eventStreamId,
        int lastNRange,
        Action<IEnumerable<EventStreamReadModel>> reducerAction)
        where TState : IActorState<TState>
        where TSnapshot : IEvent
        where TRangeEvent : IEvent;
    ValueTask MapReduceActorEventStreamFromSnapshotLastNRangeAsync<TState, TSnapshot, TRangeEvent>(
        long eventStreamId,
        int lastNRange,
        Action<IEnumerable<EventStreamReadModel>> reducerAction,
        CancellationToken cancellationToken)
        where TState : IActorState<TState>
        where TSnapshot : IEvent
        where TRangeEvent : IEvent;

    ValueTask<ICollection<EventStreamReadModel>> LoadActorEventStreamAsync<TState>(long eventStreamId) 
        where TState : IActorState<TState>;

    ValueTask<ICollection<EventStreamReadModel>> LoadActorEventStreamAsync<TState, TEvent>(long eventStreamId, int lastNRange)
        where TState : IActorState<TState> where TEvent : IEvent;

    ValueTask<ICollection<EventStreamReadModel>> LoadActorEventStreamAsync<TState, TSnapshot>(long eventStreamId)
        where TState : IActorState<TState> where TSnapshot : IEvent;

}
