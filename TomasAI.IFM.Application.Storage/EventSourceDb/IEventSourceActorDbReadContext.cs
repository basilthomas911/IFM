using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventProjector.ReadModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Application.Storage.EventSourceDb;

/// <summary>
/// Defines asynchronous actor event-source database queries.
/// </summary>
public interface IEventSourceActorDbReadContext
{
    /// <summary>Get Event Stream Id From Db using the event-source persistence services.</summary>
    /// <param name="eventStream">The event stream name.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    Task<EventStreamIdReadModel?> GetEventStreamIdFromDbAsync(string eventStream);
    /// <summary>Reads committed stream metadata without loading or replaying event payloads.</summary>
    /// <param name="eventStreamId">The event stream identifier.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    Task<long> GetCommittedStreamVersionAsync(long eventStreamId, CancellationToken cancellationToken = default);
    /// <summary>Get Command Log using the event-source persistence services.</summary>
    /// <param name="commandId">The originating command identifier.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    Task<CommandLogReadModel?> GetCommandLogAsync(Guid commandId);
    /// <summary>Has Event For Command using the event-source persistence services.</summary>
    /// <param name="commandId">The originating command identifier.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    Task<bool> HasEventForCommandAsync(Guid commandId);

    /// <summary>Get Event Projector State using the event-source persistence services.</summary>
    /// <param name="eventId">The source event identifier.</param>
    /// <param name="projectorName">The projector identity.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    Task<EventProjectorStateReadModel?> GetEventProjectorStateAsync(long eventId, string projectorName);
    /// <summary>Get Event Projector State using the event-source persistence services.</summary>
    /// <param name="eventId">The source event identifier.</param>
    /// <param name="projectorName">The projector identity.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    Task<EventProjectorStateReadModel?> GetEventProjectorStateAsync(
        long eventId,
        string projectorName,
        CancellationToken cancellationToken);
    /// <summary>Get Event Log By Event Id using the event-source persistence services.</summary>
    /// <param name="eventId">The source event identifier.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    Task<EventLogReadModel?> GetEventLogByEventIdAsync(
        long eventId,
        CancellationToken cancellationToken = default);
    /// <summary>Get Event Projector Execution State using the event-source persistence services.</summary>
    /// <param name="eventId">The source event identifier.</param>
    /// <param name="projectorName">The projector identity.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    Task<EventProjectorExecutionStateReadModel?> GetEventProjectorExecutionStateAsync(
        long eventId,
        string projectorName,
        CancellationToken cancellationToken = default);
    /// <summary>Get Event Projector Stream Checkpoint using the event-source persistence services.</summary>
    /// <param name="projectorName">The projector identity.</param>
    /// <param name="eventStreamId">The event stream identifier.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    Task<EventProjectorStreamCheckpointReadModel?> GetEventProjectorStreamCheckpointAsync(
        string projectorName,
        long eventStreamId,
        CancellationToken cancellationToken = default);
    /// <summary>Has Earlier Unresolved Event Projector Execution using the event-source persistence services.</summary>
    /// <param name="eventId">The source event identifier.</param>
    /// <param name="projectorName">The projector identity.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    Task<bool> HasEarlierUnresolvedEventProjectorExecutionAsync(
        long eventId,
        string projectorName,
        CancellationToken cancellationToken = default);
    /// <summary>Get Event Projector Operational State Page using the event-source persistence services.</summary>
    /// <param name="projectorName">The projector identity.</param>
    /// <param name="status">The requested projector or outbox status.</param>
    /// <param name="afterEventId">The exclusive event identifier page cursor.</param>
    /// <param name="batchSize">The maximum number of records to return or claim.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    Task<IReadOnlyList<EventProjectorExecutionStateReadModel>> GetEventProjectorOperationalStatePageAsync(
        string projectorName,
        EventProjectorOperationalStatus status,
        long afterEventId,
        int batchSize,
        CancellationToken cancellationToken = default);
    /// <summary>Get Event Projector Operational Snapshot using the event-source persistence services.</summary>
    /// <param name="projectorName">The projector identity.</param>
    /// <param name="nowUtc">The current UTC timestamp.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    Task<EventProjectorOperationalSnapshotReadModel> GetEventProjectorOperationalSnapshotAsync(
        string projectorName,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);
    /// <summary>Get Event Projector Recovery Page using the event-source persistence services.</summary>
    /// <param name="projectorName">The projector identity.</param>
    /// <param name="eventNames">The event names to include.</param>
    /// <param name="afterEventId">The exclusive event identifier page cursor.</param>
    /// <param name="nowUtc">The current UTC timestamp.</param>
    /// <param name="batchSize">The maximum number of records to return or claim.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    Task<IReadOnlyList<EventProjectorRecoveryItemReadModel>> GetEventProjectorRecoveryPageAsync(
        string projectorName,
        IReadOnlyCollection<string> eventNames,
        long afterEventId,
        DateTime nowUtc,
        int batchSize,
        CancellationToken cancellationToken = default);
    /// <summary>Get Uncompleted Event Projector Events using the event-source persistence services.</summary>
    /// <param name="projectorName">The projector identity.</param>
    /// <param name="eventNames">The event names to include.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    Task<ICollection<EventLogReadModel>> GetUncompletedEventProjectorEventsAsync(
        string projectorName,
        IReadOnlyCollection<string> eventNames);
    /// <summary>Get Uncompleted Event Projector Events using the event-source persistence services.</summary>
    /// <param name="projectorName">The projector identity.</param>
    /// <param name="eventNames">The event names to include.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The typed result of the event-source operation.</returns>
    Task<ICollection<EventLogReadModel>> GetUncompletedEventProjectorEventsAsync(
        string projectorName,
        IReadOnlyCollection<string> eventNames,
        CancellationToken cancellationToken);

    /// <summary>Reads the latest source snapshot and the subsequent events in its stream.</summary>
    /// <param name="eventStreamId">The event stream identifier.</param>
    /// <typeparam name="TSnapshot">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the event-source operation.</returns>
    ValueTask<ICollection<EventStreamReadModel>> GetEventsFromSnapshotAsync<TSnapshot>(long eventStreamId)
        where TSnapshot : IEvent;

    /// <summary>Map Reduce Actor Event Stream using the event-source persistence services.</summary>
    /// <param name="eventStreamId">The event stream identifier.</param>
    /// <param name="reducerAction">The reducer applied to the loaded stream events.</param>
    /// <typeparam name="TState">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The completion of the persistence operation.</returns>
    ValueTask MapReduceActorEventStreamAsync<TState>(
        long eventStreamId,
        Action<IEnumerable<EventStreamReadModel>> reducerAction)
        where TState : IActorState<TState>;
    /// <summary>Map Reduce Actor Event Stream using the event-source persistence services.</summary>
    /// <param name="eventStreamId">The event stream identifier.</param>
    /// <param name="reducerAction">The reducer applied to the loaded stream events.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <typeparam name="TState">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The completion of the persistence operation.</returns>
    ValueTask MapReduceActorEventStreamAsync<TState>(
        long eventStreamId,
        Action<IEnumerable<EventStreamReadModel>> reducerAction,
        CancellationToken cancellationToken)
        where TState : IActorState<TState>;
    /// <summary>Map Reduce Actor Event Stream using the event-source persistence services.</summary>
    /// <param name="eventStreamId">The event stream identifier.</param>
    /// <param name="lastNRange">The number of recent matching events to load.</param>
    /// <param name="reducerAction">The reducer applied to the loaded stream events.</param>
    /// <typeparam name="TState">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <typeparam name="TEvent">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The completion of the persistence operation.</returns>
    ValueTask MapReduceActorEventStreamAsync<TState, TEvent>(
        long eventStreamId,
        int lastNRange,
        Action<IEnumerable<EventStreamReadModel>> reducerAction)
        where TState : IActorState<TState>
        where TEvent : IEvent;
    /// <summary>Map Reduce Actor Event Stream using the event-source persistence services.</summary>
    /// <param name="eventStreamId">The event stream identifier.</param>
    /// <param name="lastNRange">The number of recent matching events to load.</param>
    /// <param name="reducerAction">The reducer applied to the loaded stream events.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <typeparam name="TState">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <typeparam name="TEvent">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The completion of the persistence operation.</returns>
    ValueTask MapReduceActorEventStreamAsync<TState, TEvent>(
        long eventStreamId,
        int lastNRange,
        Action<IEnumerable<EventStreamReadModel>> reducerAction,
        CancellationToken cancellationToken)
        where TState : IActorState<TState>
        where TEvent : IEvent;
    /// <summary>Map Reduce Actor Event Stream using the event-source persistence services.</summary>
    /// <param name="eventStreamId">The event stream identifier.</param>
    /// <param name="reducerAction">The reducer applied to the loaded stream events.</param>
    /// <typeparam name="TState">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <typeparam name="TSnapshot">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The completion of the persistence operation.</returns>
    ValueTask MapReduceActorEventStreamAsync<TState, TSnapshot>(
        long eventStreamId,
        Action<IEnumerable<EventStreamReadModel>> reducerAction)
        where TState : IActorState<TState>
        where TSnapshot : IEvent;
    /// <summary>Map Reduce Actor Event Stream using the event-source persistence services.</summary>
    /// <param name="eventStreamId">The event stream identifier.</param>
    /// <param name="reducerAction">The reducer applied to the loaded stream events.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <typeparam name="TState">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <typeparam name="TSnapshot">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The completion of the persistence operation.</returns>
    ValueTask MapReduceActorEventStreamAsync<TState, TSnapshot>(
        long eventStreamId,
        Action<IEnumerable<EventStreamReadModel>> reducerAction,
        CancellationToken cancellationToken)
        where TState : IActorState<TState>
        where TSnapshot : IEvent;
    /// <summary>Map Reduce Actor Event Stream From Snapshot Last NRange using the event-source persistence services.</summary>
    /// <param name="eventStreamId">The event stream identifier.</param>
    /// <param name="lastNRange">The number of recent matching events to load.</param>
    /// <param name="reducerAction">The reducer applied to the loaded stream events.</param>
    /// <typeparam name="TState">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <typeparam name="TSnapshot">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <typeparam name="TRangeEvent">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The completion of the persistence operation.</returns>
    ValueTask MapReduceActorEventStreamFromSnapshotLastNRangeAsync<TState, TSnapshot, TRangeEvent>(
        long eventStreamId,
        int lastNRange,
        Action<IEnumerable<EventStreamReadModel>> reducerAction)
        where TState : IActorState<TState>
        where TSnapshot : IEvent
        where TRangeEvent : IEvent;
    /// <summary>Map Reduce Actor Event Stream From Snapshot Last NRange using the event-source persistence services.</summary>
    /// <param name="eventStreamId">The event stream identifier.</param>
    /// <param name="lastNRange">The number of recent matching events to load.</param>
    /// <param name="reducerAction">The reducer applied to the loaded stream events.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <typeparam name="TState">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <typeparam name="TSnapshot">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <typeparam name="TRangeEvent">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The completion of the persistence operation.</returns>
    ValueTask MapReduceActorEventStreamFromSnapshotLastNRangeAsync<TState, TSnapshot, TRangeEvent>(
        long eventStreamId,
        int lastNRange,
        Action<IEnumerable<EventStreamReadModel>> reducerAction,
        CancellationToken cancellationToken)
        where TState : IActorState<TState>
        where TSnapshot : IEvent
        where TRangeEvent : IEvent;

    /// <summary>Load Actor Event Stream using the event-source persistence services.</summary>
    /// <param name="eventStreamId">The event stream identifier.</param>
    /// <typeparam name="TState">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the event-source operation.</returns>
    ValueTask<ICollection<EventStreamReadModel>> LoadActorEventStreamAsync<TState>(long eventStreamId)
        where TState : IActorState<TState>;
    /// <summary>Load Actor Event Stream using the event-source persistence services.</summary>
    /// <param name="eventStreamId">The event stream identifier.</param>
    /// <param name="lastNRange">The number of recent matching events to load.</param>
    /// <typeparam name="TState">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <typeparam name="TEvent">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the event-source operation.</returns>
    ValueTask<ICollection<EventStreamReadModel>> LoadActorEventStreamAsync<TState, TEvent>(
        long eventStreamId,
        int lastNRange)
        where TState : IActorState<TState>
        where TEvent : IEvent;
    /// <summary>Load Actor Event Stream using the event-source persistence services.</summary>
    /// <param name="eventStreamId">The event stream identifier.</param>
    /// <typeparam name="TState">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <typeparam name="TSnapshot">The event, snapshot, state or record type used by the operation.</typeparam>
    /// <returns>The typed result of the event-source operation.</returns>
    ValueTask<ICollection<EventStreamReadModel>> LoadActorEventStreamAsync<TState, TSnapshot>(long eventStreamId)
        where TState : IActorState<TState>
        where TSnapshot : IEvent;
}
