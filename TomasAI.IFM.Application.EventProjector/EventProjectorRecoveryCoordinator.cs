using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventProjector.ReadModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.EventProjector;

/// <summary>
/// Pages and durably queues recoverable projector events with bounded cross-stream concurrency.
/// Events from one source stream are always published sequentially in event-ID order.
/// </summary>
public sealed class EventProjectorRecoveryCoordinator(
    IEventSourceActorDbContext eventSource,
    IDurableReplayQueue durableQueue,
    IBlackboardService blackboard,
    EventProjectorReliabilityOptions options,
    ILogger logger)
{
    readonly IEventSourceActorDbContext _eventSource = eventSource ?? throw new ArgumentNullException(nameof(eventSource));
    readonly IDurableReplayQueue _durableQueue = durableQueue ?? throw new ArgumentNullException(nameof(durableQueue));
    readonly IBlackboardService _blackboard = blackboard ?? throw new ArgumentNullException(nameof(blackboard));
    readonly EventProjectorReliabilityOptions _options = (options ?? throw new ArgumentNullException(nameof(options))).Validate();
    readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<EventProjectorRecoveryResult> RecoverAsync(
        string actorName,
        string projectorName,
        IReadOnlyCollection<Type> projectedEventTypes,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorName);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectorName);
        ArgumentNullException.ThrowIfNull(projectedEventTypes);
        var eventNames = projectedEventTypes
            .Select(eventType => eventType.Name)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (eventNames.Length == 0)
            return new EventProjectorRecoveryResult(0, 0, 0, 0);

        long afterEventId = 0;
        long discovered = 0;
        long queued = 0;
        long claimConflicts = 0;
        long terminalFailures = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = await _eventSource.GetEventProjectorRecoveryPageAsync(
                projectorName,
                eventNames,
                afterEventId,
                DateTime.UtcNow,
                _options.RecoveryBatchSize,
                cancellationToken).ConfigureAwait(false);
            if (page.Count == 0)
                break;

            discovered += page.Count;
            afterEventId = page[^1].State.EventId;
            var streamGroups = page
                .GroupBy(item => item.State.EventStreamId)
                .Select(group => group.OrderBy(item => item.State.EventId).ToArray())
                .ToArray();
            await Parallel.ForEachAsync(
                streamGroups,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = _options.RecoveryStreamConcurrency,
                    CancellationToken = cancellationToken
                },
                async (stream, token) =>
                {
                    foreach (var item in stream)
                    {
                        token.ThrowIfCancellationRequested();
                        var domainEvent = item.EventLog.ToDomainEvent();
                        if (domainEvent is UnknownEvent)
                        {
                            var executionToken = Guid.NewGuid();
                            var nowUtc = DateTime.UtcNow;
                            var claimed = await _eventSource.TryClaimEventProjectorExecutionAsync(
                                item.State.EventId,
                                projectorName,
                                executionToken,
                                nowUtc,
                                _options.ClaimLeaseDuration,
                                token).ConfigureAwait(false);
                            if (claimed is null)
                            {
                                Interlocked.Increment(ref claimConflicts);
                                continue;
                            }
                            var terminal = await _eventSource.TryTerminalizeEventProjectorExecutionAsync(
                                new EventProjectorStateTransition(
                                    claimed.EventId,
                                    claimed.ProjectorName,
                                    executionToken,
                                    claimed.Revision,
                                    claimed.Stage,
                                    EventProjectorStageType.Completed,
                                    EventProjectorOutcomeType.Failed,
                                    claimed.LastCompletedStage,
                                    claimed.RetryCount,
                                    LastErrorAtUtc: nowUtc,
                                    ErrorMessage: $"Unable to deserialize event '{item.EventLog.EventName}' from event log version {claimed.EventId}.",
                                    BlockedReason: "unknown-source-event"),
                                nowUtc,
                                token).ConfigureAwait(false);
                            if (terminal is not null)
                                Interlocked.Increment(ref terminalFailures);
                            _logger.LogError(
                                "Unable to recover event {EventId} ({EventName}) for projector {ProjectorName}.",
                                claimed.EventId,
                                item.EventLog.EventName,
                                projectorName);
                            continue;
                        }

                        _blackboard.EventSourcing.EventProjectorState.Set(
                            item.State.EventId,
                            projectorName,
                            ToLegacyState(item.State));
                        await _durableQueue.EnqueueAsync(projectorName, domainEvent, token).ConfigureAwait(false);
                        Interlocked.Increment(ref queued);
                    }
                }).ConfigureAwait(false);

            if (page.Count < _options.RecoveryBatchSize)
                break;
        }

        return new EventProjectorRecoveryResult(discovered, queued, claimConflicts, terminalFailures);
    }

    static EventProjectorStateReadModel ToLegacyState(EventProjectorExecutionStateReadModel state)
        => new(
            state.EventId,
            state.ActorName,
            state.ProjectorName,
            state.IsReplay,
            state.AttemptNumber,
            state.Outcome,
            state.Stage,
            state.ErrorMessage,
            state.CreatedTimestamp,
            state.UpdatedTimestamp);
}
