using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventProjector.ReadModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.EventProjector;

/// <summary>
/// Executes immutable projection descriptors behind a leased, compare-and-set projector state machine.
/// </summary>
internal sealed class EventProjectorExecutionEngine(
    IEventSourceActorDbContext eventSource,
    EventProjectorReliabilityOptions options,
    string actorName,
    string projectorName,
    Func<IEvent, CancellationToken, ValueTask> publishAsync,
    ILogger logger)
{
    readonly IEventSourceActorDbContext _eventSource = eventSource ?? throw new ArgumentNullException(nameof(eventSource));
    readonly EventProjectorReliabilityOptions _options = (options ?? throw new ArgumentNullException(nameof(options))).Validate();
    readonly string _actorName = RequireName(actorName, nameof(actorName));
    readonly string _projectorName = RequireName(projectorName, nameof(projectorName));
    readonly Func<IEvent, CancellationToken, ValueTask> _publishAsync = publishAsync ?? throw new ArgumentNullException(nameof(publishAsync));
    readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<EventProjectorExecutionStateReadModel> InitializeAsync(
        IEvent domainEvent,
        EventProjectionDescriptor descriptor,
        bool isReplay,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        ArgumentNullException.ThrowIfNull(descriptor);
        if (domainEvent.EventId <= 0)
            throw new ArgumentOutOfRangeException(nameof(domainEvent), "A persisted event ID is required.");

        var existing = await _eventSource.GetEventProjectorExecutionStateAsync(
            domainEvent.EventId,
            _projectorName,
            cancellationToken).ConfigureAwait(false);
        if (existing is not null)
            return existing;

        var nowUtc = DateTime.UtcNow;
        var initialStage = descriptor.PublishProcessingEvent
            ? EventProjectorStageType.PublishProcessingEvent
            : EventProjectorStageType.ApplyProjection;
        var initial = new EventProjectorExecutionStateReadModel(
            domainEvent.EventId,
            _actorName,
            _projectorName,
            isReplay,
            0,
            EventProjectorOutcomeType.Processing,
            initialStage,
            string.Empty,
            nowUtc,
            nowUtc,
            0,
            domainEvent.GetType().Name,
            0,
            null,
            null,
            0,
            null,
            null,
            string.Empty,
            EventProjectorStageType.None,
            nowUtc);
        return await _eventSource.TryCreateEventProjectorExecutionStateAsync(initial, cancellationToken).ConfigureAwait(false)
            ?? await _eventSource.GetEventProjectorExecutionStateAsync(
                domainEvent.EventId,
                _projectorName,
                cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Unable to initialize projector state for event {domainEvent.EventId}.");
    }

    public async Task ExecuteAsync(
        IEvent domainEvent,
        EventProjectionDescriptor descriptor,
        CancellationToken cancellationToken = default)
    {
        var state = await InitializeAsync(domainEvent, descriptor, isReplay: false, cancellationToken).ConfigureAwait(false);
        if (IsTerminal(state))
            return;

        var executionToken = Guid.NewGuid();
        state = await _eventSource.TryClaimEventProjectorExecutionAsync(
            domainEvent.EventId,
            _projectorName,
            executionToken,
            DateTime.UtcNow,
            _options.ClaimLeaseDuration,
            cancellationToken).ConfigureAwait(false);
        if (state is null)
        {
            _logger.LogDebug(
                "Projection claim was not acquired for event {EventId} and projector {ProjectorName}.",
                domainEvent.EventId,
                _projectorName);
            return;
        }

        try
        {
            while (!IsTerminal(state))
            {
                state = state.Stage switch
                {
                    EventProjectorStageType.ValidateSourceEvent => await TransitionAsync(
                        state,
                        executionToken,
                        descriptor.PublishProcessingEvent
                            ? EventProjectorStageType.PublishProcessingEvent
                            : EventProjectorStageType.ApplyProjection,
                        EventProjectorStageType.ValidateSourceEvent,
                        cancellationToken: cancellationToken).ConfigureAwait(false),
                    EventProjectorStageType.PublishProcessingEvent => await PublishProcessingAsync(
                        domainEvent,
                        descriptor,
                        state,
                        executionToken,
                        cancellationToken).ConfigureAwait(false),
                    EventProjectorStageType.ApplyProjection => await ApplyProjectionAsync(
                        domainEvent,
                        descriptor,
                        state,
                        executionToken,
                        cancellationToken).ConfigureAwait(false),
                    EventProjectorStageType.PublishCompletedEvent => await PublishCompletedAsync(
                        domainEvent,
                        descriptor,
                        state,
                        executionToken,
                        cancellationToken).ConfigureAwait(false),
                    EventProjectorStageType.PublishFailedEvent => await PublishFailedAsync(
                        domainEvent,
                        descriptor,
                        state,
                        executionToken,
                        cancellationToken).ConfigureAwait(false),
                    EventProjectorStageType.PersistCompletion => await TerminalizeAsync(
                        state,
                        executionToken,
                        EventProjectorOutcomeType.Completed,
                        EventProjectorStageType.PersistCompletion,
                        cancellationToken: cancellationToken).ConfigureAwait(false),
                    _ => throw new InvalidOperationException(
                        $"Unsupported projector stage {state.Stage} for event {state.EventId}.")
                };
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            await TryReleaseAsync(state, executionToken, ex, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    public async Task TerminalizeUnregisteredAsync(
        IEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        var state = await _eventSource.GetEventProjectorExecutionStateAsync(
            domainEvent.EventId,
            _projectorName,
            cancellationToken).ConfigureAwait(false);
        if (state is null || IsTerminal(state))
            return;

        var executionToken = Guid.NewGuid();
        state = await _eventSource.TryClaimEventProjectorExecutionAsync(
            domainEvent.EventId,
            _projectorName,
            executionToken,
            DateTime.UtcNow,
            _options.ClaimLeaseDuration,
            cancellationToken).ConfigureAwait(false);
        if (state is null)
            return;

        _ = await TerminalizeAsync(
            state,
            executionToken,
            EventProjectorOutcomeType.Failed,
            state.LastCompletedStage,
            $"Event type '{domainEvent.GetType().FullName}' is not registered by projector '{_projectorName}'.",
            "unregistered-source-event",
            cancellationToken).ConfigureAwait(false);
    }

    public async Task HandleMaximumAttemptsAsync(IEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var state = await _eventSource.GetEventProjectorExecutionStateAsync(
            domainEvent.EventId,
            _projectorName,
            cancellationToken).ConfigureAwait(false);
        if (state is null || IsTerminal(state))
            return;

        var executionToken = Guid.NewGuid();
        state = await _eventSource.TryClaimEventProjectorExecutionAsync(
            domainEvent.EventId,
            _projectorName,
            executionToken,
            DateTime.UtcNow,
            _options.ClaimLeaseDuration,
            cancellationToken).ConfigureAwait(false);
        if (state is null)
            return;

        _ = await TerminalizeAsync(
            state,
            executionToken,
            EventProjectorOutcomeType.Failed,
            state.LastCompletedStage,
            $"Maximum {_options.MaximumReplayAttempts} attempts reached for event {domainEvent.EventId} of type {domainEvent.GetType().Name}.",
            "maximum-attempts-reached",
            cancellationToken).ConfigureAwait(false);
    }

    async Task<EventProjectorExecutionStateReadModel> PublishProcessingAsync(
        IEvent domainEvent,
        EventProjectionDescriptor descriptor,
        EventProjectorExecutionStateReadModel state,
        Guid executionToken,
        CancellationToken cancellationToken)
    {
        if (descriptor.PublishProcessingEvent)
            await _publishAsync(domainEvent, cancellationToken).ConfigureAwait(false);
        return await TransitionAsync(
            state,
            executionToken,
            EventProjectorStageType.ApplyProjection,
            EventProjectorStageType.PublishProcessingEvent,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    async Task<EventProjectorExecutionStateReadModel> ApplyProjectionAsync(
        IEvent domainEvent,
        EventProjectionDescriptor descriptor,
        EventProjectorExecutionStateReadModel state,
        Guid executionToken,
        CancellationToken cancellationToken)
    {
        var context = new ProjectionExecutionContext(
            _projectorName,
            state.EventId,
            state.EventStreamId,
            new EventProjectorEffectIdentity(
                _projectorName,
                state.EventId,
                EventProjectorEffectKind.TargetProjection),
            executionToken,
            descriptor.IdempotencyStrategy,
            cancellationToken);
        var result = await descriptor.ApplyAsync(domainEvent, context).ConfigureAwait(false);
        ArgumentNullException.ThrowIfNull(result);

        if (result.Outcome == EventProjectionApplyOutcome.Superseded)
        {
            return await TerminalizeAsync(
                state,
                executionToken,
                EventProjectorOutcomeType.Superseded,
                EventProjectorStageType.ApplyProjection,
                result.ErrorMessage,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return await TransitionAsync(
            state,
            executionToken,
            result.Success
                ? EventProjectorStageType.PublishCompletedEvent
                : EventProjectorStageType.PublishFailedEvent,
            EventProjectorStageType.ApplyProjection,
            result.ErrorMessage,
            cancellationToken).ConfigureAwait(false);
    }

    async Task<EventProjectorExecutionStateReadModel> PublishCompletedAsync(
        IEvent domainEvent,
        EventProjectionDescriptor descriptor,
        EventProjectorExecutionStateReadModel state,
        Guid executionToken,
        CancellationToken cancellationToken)
    {
        var completedEvent = descriptor.CompletedEventFactory(domainEvent)
            ?? throw new InvalidOperationException(
                $"The completion-event factory returned null for {domainEvent.GetType().Name}.");
        await _publishAsync(completedEvent, cancellationToken).ConfigureAwait(false);
        return await TerminalizeAsync(
            state,
            executionToken,
            EventProjectorOutcomeType.Completed,
            EventProjectorStageType.PublishCompletedEvent,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    async Task<EventProjectorExecutionStateReadModel> PublishFailedAsync(
        IEvent domainEvent,
        EventProjectionDescriptor descriptor,
        EventProjectorExecutionStateReadModel state,
        Guid executionToken,
        CancellationToken cancellationToken)
    {
        var failure = new InvalidOperationException(state.ErrorMessage);
        var failedEvent = descriptor.FailedEventFactory(domainEvent, failure);
        if (failedEvent is null)
        {
            return await TerminalizeAsync(
                state,
                executionToken,
                EventProjectorOutcomeType.Failed,
                EventProjectorStageType.ApplyProjection,
                "The failure-event factory returned null.",
                "failed-event-conversion",
                cancellationToken).ConfigureAwait(false);
        }

        await _publishAsync(failedEvent, cancellationToken).ConfigureAwait(false);
        return await TerminalizeAsync(
            state,
            executionToken,
            EventProjectorOutcomeType.Failed,
            EventProjectorStageType.PublishFailedEvent,
            state.ErrorMessage,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    async Task<EventProjectorExecutionStateReadModel> TransitionAsync(
        EventProjectorExecutionStateReadModel state,
        Guid executionToken,
        EventProjectorStageType nextStage,
        EventProjectorStageType lastCompletedStage,
        string errorMessage = "",
        CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        return await _eventSource.TryTransitionEventProjectorExecutionAsync(
            new EventProjectorStateTransition(
                state.EventId,
                state.ProjectorName,
                executionToken,
                state.Revision,
                state.Stage,
                nextStage,
                EventProjectorOutcomeType.Processing,
                lastCompletedStage,
                state.RetryCount,
                ErrorMessage: errorMessage),
            nowUtc,
            cancellationToken).ConfigureAwait(false)
            ?? throw LostFence(state);
    }

    async Task<EventProjectorExecutionStateReadModel> TerminalizeAsync(
        EventProjectorExecutionStateReadModel state,
        Guid executionToken,
        EventProjectorOutcomeType outcome,
        EventProjectorStageType lastCompletedStage,
        string errorMessage = "",
        string blockedReason = "",
        CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        return await _eventSource.TryTerminalizeEventProjectorExecutionAsync(
            new EventProjectorStateTransition(
                state.EventId,
                state.ProjectorName,
                executionToken,
                state.Revision,
                state.Stage,
                EventProjectorStageType.Completed,
                outcome,
                lastCompletedStage,
                state.RetryCount,
                LastErrorAtUtc: outcome == EventProjectorOutcomeType.Failed ? nowUtc : null,
                ErrorMessage: errorMessage,
                BlockedReason: blockedReason),
            nowUtc,
            cancellationToken).ConfigureAwait(false)
            ?? throw LostFence(state);
    }

    async Task TryReleaseAsync(
        EventProjectorExecutionStateReadModel state,
        Guid executionToken,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (IsTerminal(state))
            return;
        var nowUtc = DateTime.UtcNow;
        var retryCount = state.RetryCount + 1;
        try
        {
            _ = await _eventSource.TryReleaseEventProjectorExecutionAsync(
                new EventProjectorStateTransition(
                    state.EventId,
                    state.ProjectorName,
                    executionToken,
                    state.Revision,
                    state.Stage,
                    state.Stage,
                    EventProjectorOutcomeType.Retrying,
                    state.LastCompletedStage,
                    retryCount,
                    nowUtc.Add(GetRetryDelay(retryCount)),
                    nowUtc,
                    exception.Message),
                nowUtc,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception releaseException)
        {
            _logger.LogWarning(
                releaseException,
                "Unable to release projection claim for event {EventId} and projector {ProjectorName}.",
                state.EventId,
                state.ProjectorName);
        }
    }

    TimeSpan GetRetryDelay(int retryCount)
    {
        var exponent = Math.Clamp(retryCount - 1, 0, 6);
        var milliseconds = _options.InitialReplayDelay.TotalMilliseconds * Math.Pow(2, exponent);
        return TimeSpan.FromMilliseconds(Math.Min(milliseconds, TimeSpan.FromMinutes(2).TotalMilliseconds));
    }

    static bool IsTerminal(EventProjectorExecutionStateReadModel state)
        => state.Stage == EventProjectorStageType.Completed
            || state.Outcome is EventProjectorOutcomeType.Completed
                or EventProjectorOutcomeType.Failed
                or EventProjectorOutcomeType.Cancelled
                or EventProjectorOutcomeType.Superseded
                or EventProjectorOutcomeType.AlreadyCompleted;

    InvalidOperationException LostFence(EventProjectorExecutionStateReadModel state)
        => new($"Projection fence was lost for event {state.EventId}, projector '{state.ProjectorName}', stage {state.Stage}, revision {state.Revision}.");

    static string RequireName(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value;
    }
}
