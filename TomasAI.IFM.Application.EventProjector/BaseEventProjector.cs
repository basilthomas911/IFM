using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Reflection;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventProjector.ReadModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.EventProjector;

/// <summary>
/// Provides common event-projector behavior for a specific command actor type.
/// </summary>
/// <typeparam name="TActor">
/// The command actor type associated with the projection.
/// </typeparam>
/// <param name="logger">
/// The logger used by the projector for operational and diagnostic messages.
/// </param>
/// <remarks>
/// The base implementation owns both delivery lanes. Descriptors use the durable process/replay queue by default;
/// descriptors that explicitly opt out run once through a bounded process-local queue.
/// </remarks>
public abstract class BaseEventProjector<TActor> (
    IDurableReplayQueue durableReplayQueue,
    IEventSourceActorDbContext dbEventSource,
    IBlackboardService blackboardService,
    ILogger logger,
    EventProjectorReliabilityOptions? reliabilityOptions = null): IEventProjector<TActor>, IEventProjectorReadiness
    where TActor : ICommandActor<TActor>
{
    readonly EventProjectorReliabilityOptions _reliabilityOptions =
        (reliabilityOptions ?? new EventProjectorReliabilityOptions()).Validate();
    readonly object _descriptorLock = new();
    FrozenDictionary<Type, EventProjectionDescriptor>? _descriptorMap;
    EventProjectorExecutionEngine? _executionEngine;
    EventProjectorOutboxDispatcher? _outboxDispatcher;
    EventProjectorMetricsObserver? _metricsObserver;
    IEventProjectorTransientQueue? _transientQueue;
    static readonly ConcurrentDictionary<Type, Func<ICommandActorContext, IEvent, CancellationToken, ValueTask>>
        EventPublishers = new();
    ICommandActorContext? _context;
    EventProjectorReadinessSnapshot _readiness = new(
        string.Empty,
        false,
        0,
        0,
        DateTimeOffset.MinValue);

    public abstract string ActorName { get; }
    public abstract string ProjectorName { get; }
    public abstract string DurableProcessQueueName { get; }
    public abstract string DurableReplayQueueName { get; }
    public abstract IReadOnlyCollection<Type> ProjectedEventTypes { get; }
    public abstract IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors { get; }

    public EventProjectorReadinessSnapshot Readiness => Volatile.Read(ref _readiness) with
    {
        ProjectorName = ProjectorName
    };

    public EventProjectorReadinessSnapshot GetSnapshot(string projectorName)
    {
        if (!string.Equals(projectorName, ProjectorName, StringComparison.Ordinal))
            throw new ArgumentException($"Projector '{projectorName}' is not owned by this readiness source.", nameof(projectorName));
        return Readiness;
    }

    /// <inheritdoc />
    public virtual async ValueTask ProcessDomainEventAsync(IEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        if (!GetDescriptorMap().TryGetValue(domainEvent.GetType(), out var descriptor))
        {
            if (_reliabilityOptions.FencedExecutionEnabled)
                await ExecutionEngine.TerminalizeUnregisteredAsync(domainEvent).ConfigureAwait(false);
            else
                await TerminalizeLegacyUnregisteredAsync(domainEvent).ConfigureAwait(false);
            return;
        }

        if (!descriptor.UseDurableReplay)
        {
            await ExecuteTransientDescriptorAsync(domainEvent, descriptor, CancellationToken.None)
                .ConfigureAwait(false);
            return;
        }

        if (_reliabilityOptions.FencedExecutionEnabled)
            await ExecutionEngine.ExecuteAsync(domainEvent, descriptor).ConfigureAwait(false);
        else
            await ExecuteLegacyDescriptorAsync(domainEvent, descriptor).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the database context for event sourcing operations.
    /// </summary>
    public IEventSourceActorDbContext DbEventSource { get; init; } = IsArgumentNull.Set(dbEventSource);

    /// <summary>
    /// Gets the durable replay queue used for event replay operations.
    /// </summary>
    public IDurableReplayQueue DurableReplayQueue { get; init; } = IsArgumentNull.Set(durableReplayQueue);

    public IBlackboardService BlackboardService { get; init; } = IsArgumentNull.Set(blackboardService);

    /// <summary>
    /// Gets the runtime context of the command actor that started this projector.
    /// </summary>
    /// <exception cref="InvalidOperationException">The projector has not been started.</exception>
    public ICommandActorContext Context => _context
        ?? throw new InvalidOperationException($"Projector '{ProjectorName}' has not been started.");

    /// <summary>
    /// Gets the logger used for operational and diagnostic messages.
    /// </summary>
    public ILogger Logger { get; init; }  = IsArgumentNull.Set(logger);

    /// <summary>
    /// Starts the durable and/or non-durable workers required by the projector's descriptors.
    /// </summary>
    /// <param name="context">The runtime context created for the command actor that owns this projector.</param>
    /// <param name="cancellationToken">A token that cancels startup and the workers started by this call.</param>
    /// <returns>A task-like value that represents the asynchronous startup operation.</returns>
    /// <remarks>
    /// The handler is registered before the queue is started so that recovered durable messages cannot be
    /// consumed before the projector is ready to process them. Call this method from the owning command actor's
    /// startup lifecycle rather than once per projected event batch.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public async ValueTask StartAsync(
        ICommandActorContext context,
        CancellationToken cancellationToken = default)
    {
        _context = IsArgumentNull.Set(context);
        var descriptors = GetDescriptorMap().Values;
        var hasDurableDescriptors = descriptors.Any(static descriptor => descriptor.UseDurableReplay);
        var hasTransientDescriptors = descriptors.Any(static descriptor => !descriptor.UseDurableReplay);
        SetReadiness(false, 0, 0);
        var startupStarted = EventProjectorMetrics.GetStartupTimestamp();
        try
        {
            var workerCapacity = (hasDurableDescriptors ? 2 : 0)
                + (hasTransientDescriptors ? 1 : 0)
                + (hasDurableDescriptors && _reliabilityOptions.TransactionalOutboxEnabled ? 1 : 0);
            EventProjectorMetrics.RegisterProjector(
                ProjectorName,
                workerCapacity);

            if (hasTransientDescriptors)
            {
                var transientQueue = _transientQueue ??= new EventProjectorTransientQueue(
                    ProjectorName,
                    _reliabilityOptions.NonDurableQueueCapacity,
                    Logger);
                await transientQueue.StartAsync(
                    ProcessTransientQueuedDomainEventAsync,
                    cancellationToken).ConfigureAwait(false);
            }

            var recovery = new EventProjectorRecoveryResult(0, 0, 0, 0);
            if (hasDurableDescriptors)
            {
                DurableReplayQueue.SetMaxReplayAttemps(
                    ProjectorName,
                    _reliabilityOptions.MaximumReplayAttempts);
                DurableReplayQueue.SetMaxAttemptsReachedAction(
                    ProjectorName,
                    HandleMaximumAttemptsAsync);
                await DurableReplayQueue.PrepareAsync(
                    ProjectorName,
                    _reliabilityOptions.InitialReplayDelay,
                    cancellationToken).ConfigureAwait(false);
                await DurableReplayQueue.DequeueAsync(
                    ProjectorName,
                    ProcessQueuedDomainEventAsync,
                    cancellationToken).ConfigureAwait(false);
                var durableEventTypes = descriptors
                    .Where(static descriptor => descriptor.UseDurableReplay)
                    .Select(static descriptor => descriptor.SourceEventType)
                    .ToArray();
                recovery = _reliabilityOptions.BoundedRecoveryEnabled
                    ? await CreateRecoveryCoordinator().RecoverAsync(
                        ActorName,
                        ProjectorName,
                        durableEventTypes,
                        cancellationToken).ConfigureAwait(false)
                    : await RecoverUncompletedEventsAsync(
                        durableEventTypes,
                        cancellationToken).ConfigureAwait(false);
                if (_reliabilityOptions.TransactionalOutboxEnabled)
                    await OutboxDispatcher.StartAsync(cancellationToken).ConfigureAwait(false);
                if (_reliabilityOptions.BacklogMetricsPollingEnabled)
                    await MetricsObserver.StartAsync(cancellationToken).ConfigureAwait(false);
                await DurableReplayQueue.StartAsync(
                    ProjectorName,
                    _reliabilityOptions.InitialReplayDelay,
                    cancellationToken).ConfigureAwait(false);
            }
            SetReadiness(true, recovery.Discovered, recovery.Queued);
            EventProjectorMetrics.RecordStartup(ProjectorName, "ready", startupStarted);
        }
        catch (Exception ex)
        {
            if (_metricsObserver is not null)
            {
                try
                {
                    await _metricsObserver.StopAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception stopException)
                {
                    Logger.LogWarning(stopException, "Unable to roll back projector metrics startup for {ProjectorName}.", ProjectorName);
                }
            }
            if (_outboxDispatcher is not null)
            {
                try
                {
                    await _outboxDispatcher.StopAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception stopException)
                {
                    Logger.LogWarning(stopException, "Unable to roll back projector outbox startup for {ProjectorName}.", ProjectorName);
                }
            }
            if (hasTransientDescriptors && _transientQueue is not null)
            {
                try
                {
                    await _transientQueue.StopAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception stopException)
                {
                    Logger.LogWarning(stopException, "Unable to roll back projector transient queue startup for {ProjectorName}.", ProjectorName);
                }
            }
            if (hasDurableDescriptors)
            {
                try
                {
                    await DurableReplayQueue.StopAsync(ProjectorName, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception stopException)
                {
                    Logger.LogWarning(stopException, "Unable to roll back projector queue startup for {ProjectorName}.", ProjectorName);
                }
            }
            _context = null;
            EventProjectorMetrics.UnregisterProjector(ProjectorName);
            SetReadiness(false, 0, 0, ex.Message);
            EventProjectorMetrics.RecordStartup(ProjectorName, "failed", startupStarted);
            throw;
        }
    }

    /// <summary>
    /// Stops the projector's durable process and replay queue workers.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the wait to begin the stop operation.</param>
    /// <returns>A task-like value that represents the asynchronous shutdown operation.</returns>
    /// <remarks>
    /// Queue configuration and the registered handler are retained, allowing a later call to
    /// <see cref="StartAsync(ICommandActorContext, CancellationToken)"/> to restart the projector.
    /// </remarks>
    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        SetReadiness(false, Readiness.RecoveryEventsDiscovered, Readiness.RecoveryEventsQueued);
        var descriptors = GetDescriptorMap().Values;
        var hasDurableDescriptors = descriptors.Any(static descriptor => descriptor.UseDurableReplay);
        var hasTransientDescriptors = descriptors.Any(static descriptor => !descriptor.UseDurableReplay);
        if (hasTransientDescriptors && _transientQueue is not null)
            await _transientQueue.StopAsync(cancellationToken).ConfigureAwait(false);
        if (_outboxDispatcher is not null)
            await _outboxDispatcher.StopAsync(cancellationToken).ConfigureAwait(false);
        if (_metricsObserver is not null)
            await _metricsObserver.StopAsync(cancellationToken).ConfigureAwait(false);
        if (hasDurableDescriptors)
            await DurableReplayQueue.StopAsync(ProjectorName, cancellationToken).ConfigureAwait(false);
        EventProjectorMetrics.UnregisterProjector(ProjectorName);
    }

    /// <summary>
    /// Routes domain events to the durable or non-durable process queue selected by each descriptor.
    /// </summary>
    /// <param name="domainEvents">The domain events to enqueue for asynchronous projection.</param>
    /// <returns>A completed task-like value after all events have been accepted by their selected queues.</returns>
    /// <remarks>
    /// The projector must be started through <see cref="StartAsync(ICommandActorContext, CancellationToken)"/>
    /// during its owning actor's lifecycle.
    /// This method performs data-plane work only and does not reconfigure the queue or replace its handler.
    /// </remarks>
    public async ValueTask DomainEventsProjectionAsync(DomainEventCollection domainEvents)
    {
        foreach (var domainEvent in domainEvents)
        {
            if (!GetDescriptorMap().TryGetValue(domainEvent.GetType(), out var descriptor))
                throw new InvalidOperationException(
                    $"Event type '{domainEvent.GetType().FullName}' is not registered by projector '{ProjectorName}'.");

            if (!descriptor.UseDurableReplay)
            {
                var transientQueue = _transientQueue
                    ?? throw new InvalidOperationException(
                        $"Projector '{ProjectorName}' must be started before non-durable events can be queued.");
                await transientQueue.EnqueueAsync(domainEvent).ConfigureAwait(false);
                EventProjectorMetrics.RecordEvent(ProjectorName, "accepted", "transient");
                continue;
            }

            if (_reliabilityOptions.FencedExecutionEnabled)
            {
                var state = await ExecutionEngine.InitializeAsync(
                    domainEvent,
                    descriptor,
                    isReplay: false).ConfigureAwait(false);
                if (IsTerminal(state))
                    continue;
                await DurableReplayQueue.EnqueueAsync(ProjectorName, domainEvent).ConfigureAwait(false);
                continue;
            }

            var projectionState = CreateInitialState(domainEvent.EventId);

            // Persist the recoverable marker before publishing. If publication fails after the event-log
            // transaction committed, startup recovery will find this state and enqueue the source event again.
            await DbEventSource.InsertEventProjectorStateAsync(projectionState);
            BlackboardService.EventSourcing.EventProjectorState.Set(
                domainEvent.EventId,
                ProjectorName,
                projectionState);
            await DurableReplayQueue.EnqueueAsync(ProjectorName, domainEvent).ConfigureAwait(false);
        }
    }

    public Task<IReadOnlyList<EventProjectorExecutionStateReadModel>> GetOperationalStatesAsync(
        EventProjectorOperationalStatus status,
        long afterEventId = 0,
        int batchSize = 256,
        CancellationToken cancellationToken = default)
        => DbEventSource.GetEventProjectorOperationalStatePageAsync(
            ProjectorName, status, afterEventId, batchSize, cancellationToken);

    public async ValueTask<bool> RetryExactAsync(
        long eventId,
        CancellationToken cancellationToken = default)
    {
        var eventLog = await DbEventSource.GetEventLogByEventIdAsync(eventId, cancellationToken).ConfigureAwait(false);
        if (eventLog is null)
            return false;
        var domainEvent = eventLog.ToDomainEvent();
        if (domainEvent is UnknownEvent
            || !GetDescriptorMap().TryGetValue(domainEvent.GetType(), out var descriptor)
            || !descriptor.UseDurableReplay)
            return false;
        var state = await DbEventSource.TryRetryEventProjectorExecutionAsync(
            eventId, ProjectorName, DateTime.UtcNow, cancellationToken).ConfigureAwait(false);
        if (state is null)
            return false;
        await DurableReplayQueue.EnqueueAsync(ProjectorName, domainEvent, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async ValueTask<bool> SkipAsync(
        long eventId,
        string reason,
        CancellationToken cancellationToken = default)
        => await DbEventSource.TrySkipEventProjectorExecutionAsync(
            eventId, ProjectorName, reason, DateTime.UtcNow, cancellationToken).ConfigureAwait(false) is not null;

    async Task ProcessQueuedDomainEventAsync(IEvent domainEvent)
    {
        if (GetDescriptorMap().TryGetValue(domainEvent.GetType(), out var descriptor)
            && !descriptor.UseDurableReplay)
        {
            Logger.LogWarning(
                "Ignoring durable delivery for non-durable event {EventId} ({EventType}) in projector {ProjectorName}.",
                domainEvent.EventId,
                domainEvent.GetType().Name,
                ProjectorName);
            return;
        }

        if (_reliabilityOptions.FencedExecutionEnabled)
        {
            await ProcessDomainEventAsync(domainEvent).ConfigureAwait(false);
            return;
        }

        var currentState = BlackboardService.EventSourcing.EventProjectorState.Get(
            domainEvent.EventId,
            ProjectorName)
            ?? await DbEventSource.GetEventProjectorStateAsync(domainEvent.EventId, ProjectorName);

        if (currentState is null)
        {
            currentState = CreateInitialState(domainEvent.EventId);
            await DbEventSource.InsertEventProjectorStateAsync(currentState);
        }

        if (IsTerminal(currentState))
            return;

        BlackboardService.EventSourcing.EventProjectorState.Set(
            domainEvent.EventId,
            ProjectorName,
            currentState);
        await ProcessDomainEventAsync(domainEvent);
    }

    async ValueTask ProcessTransientQueuedDomainEventAsync(
        IEvent domainEvent,
        CancellationToken cancellationToken)
    {
        if (!GetDescriptorMap().TryGetValue(domainEvent.GetType(), out var descriptor))
        {
            EventProjectorMetrics.RecordEvent(ProjectorName, "unregistered", "transient");
            Logger.LogError(
                "Dropping unregistered non-durable event {EventId} ({EventType}) for projector {ProjectorName}.",
                domainEvent.EventId,
                domainEvent.GetType().FullName,
                ProjectorName);
            return;
        }
        if (descriptor.UseDurableReplay)
        {
            EventProjectorMetrics.RecordEvent(ProjectorName, "misrouted", "transient");
            Logger.LogError(
                "Dropping durable event {EventId} ({EventType}) routed to the non-durable queue for projector {ProjectorName}.",
                domainEvent.EventId,
                domainEvent.GetType().FullName,
                ProjectorName);
            return;
        }

        await ExecuteTransientDescriptorAsync(domainEvent, descriptor, cancellationToken).ConfigureAwait(false);
    }

    async ValueTask ExecuteTransientDescriptorAsync(
        IEvent domainEvent,
        EventProjectionDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        EventProjectorMetrics.WorkerBusy(ProjectorName);
        try
        {
            if (descriptor.PublishProcessingEvent && !descriptor.PublishProcessingAfterApply)
            {
                try
                {
                    await PublishProjectionEventAsync(domainEvent, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    EventProjectorMetrics.RecordEvent(
                        ProjectorName,
                        "processing-publication-failed",
                        "transient");
                    Logger.LogWarning(
                        ex,
                        "Non-durable processing publication failed for event {EventId} in projector {ProjectorName}; the target action will still run.",
                        domainEvent.EventId,
                        ProjectorName);
                }
            }

            EventProjectionApplyResult result;
            try
            {
                var eventStreamId = await ResolveTransientEventStreamIdAsync(
                    domainEvent,
                    cancellationToken).ConfigureAwait(false);
                if (eventStreamId <= 0)
                    throw new InvalidOperationException(
                        $"Event stream identity is missing for event {domainEvent.EventId}.");

                result = await descriptor.ApplyAsync(
                    domainEvent,
                    new ProjectionExecutionContext(
                        ProjectorName,
                        domainEvent.EventId,
                        eventStreamId,
                        new EventProjectorEffectIdentity(
                            ProjectorName,
                            domainEvent.EventId,
                            EventProjectorEffectKind.TargetProjection),
                        Guid.NewGuid(),
                        descriptor.IdempotencyStrategy,
                        cancellationToken)).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("The projection action returned no result.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                EventProjectorMetrics.RecordEvent(ProjectorName, "apply-failed", "transient");
                if (descriptor.PublishTerminalEvent)
                {
                    await PublishTransientFailureAsync(
                        domainEvent,
                        descriptor,
                        ex,
                        cancellationToken).ConfigureAwait(false);
                }
                return;
            }

            switch (result.Outcome)
            {
                case EventProjectionApplyOutcome.Applied:
                case EventProjectionApplyOutcome.AlreadyApplied:
                    if (descriptor.PublishProcessingEvent && descriptor.PublishProcessingAfterApply)
                    {
                        await PublishProjectionEventAsync(domainEvent, cancellationToken).ConfigureAwait(false);
                    }
                    if (descriptor.PublishTerminalEvent)
                    {
                        await PublishTransientCompletionAsync(
                            domainEvent,
                            descriptor,
                            cancellationToken).ConfigureAwait(false);
                    }
                    EventProjectorMetrics.RecordEvent(ProjectorName, "completed", "transient");
                    break;

                case EventProjectionApplyOutcome.Superseded:
                    EventProjectorMetrics.RecordEvent(ProjectorName, "superseded", "transient");
                    break;

                case EventProjectionApplyOutcome.Failed:
                    EventProjectorMetrics.RecordEvent(ProjectorName, "apply-failed", "transient");
                    if (descriptor.PublishTerminalEvent)
                    {
                        await PublishTransientFailureAsync(
                            domainEvent,
                            descriptor,
                            new InvalidOperationException(result.ErrorMessage),
                            cancellationToken).ConfigureAwait(false);
                    }
                    break;

                default:
                    EventProjectorMetrics.RecordEvent(ProjectorName, "apply-failed", "transient");
                    if (descriptor.PublishTerminalEvent)
                    {
                        await PublishTransientFailureAsync(
                            domainEvent,
                            descriptor,
                            new InvalidOperationException(
                                $"Unsupported projection outcome '{result.Outcome}'."),
                            cancellationToken).ConfigureAwait(false);
                    }
                    break;
            }
        }
        finally
        {
            EventProjectorMetrics.WorkerAvailable(ProjectorName);
        }
    }

    async ValueTask<long> ResolveTransientEventStreamIdAsync(
        IEvent domainEvent,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(domainEvent.AggregateId))
        {
            var eventStreamId = await DbEventSource.GetEventStreamIdAsync(
                domainEvent.AggregateId,
                cancellationToken).ConfigureAwait(false);
            if (eventStreamId > 0)
                return eventStreamId;
        }

        var eventLog = await DbEventSource.GetEventLogByEventIdAsync(
            domainEvent.EventId,
            cancellationToken).ConfigureAwait(false);
        return eventLog?.EventStreamId ?? 0;
    }

    async ValueTask PublishTransientCompletionAsync(
        IEvent domainEvent,
        EventProjectionDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        try
        {
            var completedEvent = descriptor.CompletedEventFactory(domainEvent)
                ?? throw new InvalidOperationException(
                    $"The completion-event factory returned null for {domainEvent.GetType().Name}.");
            await PublishProjectionEventAsync(completedEvent, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            EventProjectorMetrics.RecordEvent(
                ProjectorName,
                "terminal-publication-failed",
                "transient");
            Logger.LogWarning(
                ex,
                "Non-durable completion publication failed for event {EventId} in projector {ProjectorName}; it will not be replayed.",
                domainEvent.EventId,
                ProjectorName);
        }
    }

    async ValueTask PublishTransientFailureAsync(
        IEvent domainEvent,
        EventProjectionDescriptor descriptor,
        Exception failure,
        CancellationToken cancellationToken)
    {
        try
        {
            var failedEvent = descriptor.FailedEventFactory(domainEvent, failure)
                ?? throw new InvalidOperationException(
                    $"The failure-event factory returned null for {domainEvent.GetType().Name}.");
            await PublishProjectionEventAsync(failedEvent, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            EventProjectorMetrics.RecordEvent(
                ProjectorName,
                "terminal-publication-failed",
                "transient");
            Logger.LogWarning(
                ex,
                "Non-durable failure publication failed for event {EventId} in projector {ProjectorName}; it will not be replayed.",
                domainEvent.EventId,
                ProjectorName);
        }
    }

    async ValueTask ExecuteLegacyDescriptorAsync(
        IEvent domainEvent,
        EventProjectionDescriptor descriptor)
    {
        var currentState = BlackboardService.EventSourcing.EventProjectorState.Get(
                domainEvent.EventId,
                ProjectorName)
            ?? await DbEventSource.GetEventProjectorStateAsync(domainEvent.EventId, ProjectorName)
            ?? throw new InvalidOperationException(
                $"Projection state was not initialized for event {domainEvent.EventId} and projector '{ProjectorName}'.");
        BlackboardService.EventSourcing.EventProjectorState.Set(
            domainEvent.EventId,
            ProjectorName,
            currentState);

        try
        {
            while (!IsTerminal(currentState))
            {
                switch (currentState.Stage)
                {
                    case EventProjectorStageType.PublishProcessingEvent:
                        if (descriptor.PublishProcessingEvent && !descriptor.PublishProcessingAfterApply)
                            await PublishProjectionEventAsync(domainEvent, CancellationToken.None).ConfigureAwait(false);
                        currentState = currentState with
                        {
                            Stage = EventProjectorStageType.ApplyProjection,
                            Outcome = EventProjectorOutcomeType.Processing,
                            UpdatedTimestamp = DateTime.UtcNow
                        };
                        await PersistLegacyStateAsync(
                            currentState,
                            clearCache: IsTerminal(currentState)).ConfigureAwait(false);
                        break;

                    case EventProjectorStageType.ApplyProjection:
                        var executionState = await DbEventSource.GetEventProjectorExecutionStateAsync(
                            domainEvent.EventId,
                            ProjectorName).ConfigureAwait(false);
                        var eventStreamId = executionState?.EventStreamId ?? 0;
                        if (eventStreamId <= 0 && !string.IsNullOrWhiteSpace(domainEvent.AggregateId))
                            eventStreamId = await DbEventSource.GetEventStreamIdAsync(domainEvent.AggregateId).ConfigureAwait(false);
                        if (eventStreamId <= 0)
                            throw new InvalidOperationException($"Event stream identity is missing for event {domainEvent.EventId}.");

                        var applyResult = await descriptor.ApplyAsync(
                            domainEvent,
                            new ProjectionExecutionContext(
                                ProjectorName,
                                domainEvent.EventId,
                                eventStreamId,
                                new EventProjectorEffectIdentity(
                                    ProjectorName,
                                    domainEvent.EventId,
                                    EventProjectorEffectKind.TargetProjection),
                                Guid.NewGuid(),
                                descriptor.IdempotencyStrategy,
                                CancellationToken.None)).ConfigureAwait(false);
                        if (applyResult.Success && descriptor.PublishProcessingEvent &&
                            descriptor.PublishProcessingAfterApply)
                        {
                            await PublishProjectionEventAsync(domainEvent, CancellationToken.None).ConfigureAwait(false);
                        }
                        currentState = currentState with
                        {
                            Stage = applyResult.Success
                                ? descriptor.PublishTerminalEvent
                                    ? EventProjectorStageType.PublishCompletedEvent
                                    : EventProjectorStageType.Completed
                                : EventProjectorStageType.PublishFailedEvent,
                            Outcome = applyResult.Success
                                ? descriptor.PublishTerminalEvent
                                    ? EventProjectorOutcomeType.Processing
                                    : EventProjectorOutcomeType.Completed
                                : EventProjectorOutcomeType.Retrying,
                            ErrorMessage = applyResult.ErrorMessage,
                            UpdatedTimestamp = DateTime.UtcNow
                        };
                        await PersistLegacyStateAsync(
                            currentState,
                            clearCache: IsTerminal(currentState)).ConfigureAwait(false);
                        break;

                    case EventProjectorStageType.PublishCompletedEvent:
                        var completedEvent = descriptor.CompletedEventFactory(domainEvent)
                            ?? throw new InvalidOperationException(
                                $"The completion-event factory returned null for {domainEvent.GetType().Name}.");
                        await PublishProjectionEventAsync(completedEvent, CancellationToken.None).ConfigureAwait(false);
                        currentState = currentState with
                        {
                            Stage = EventProjectorStageType.Completed,
                            Outcome = EventProjectorOutcomeType.Completed,
                            UpdatedTimestamp = DateTime.UtcNow
                        };
                        await PersistLegacyStateAsync(currentState, clearCache: true).ConfigureAwait(false);
                        break;

                    case EventProjectorStageType.PublishFailedEvent:
                        if (!descriptor.PublishTerminalEvent)
                        {
                            currentState = currentState with
                            {
                                Stage = EventProjectorStageType.Completed,
                                Outcome = EventProjectorOutcomeType.Failed,
                                UpdatedTimestamp = DateTime.UtcNow
                            };
                            await PersistLegacyStateAsync(currentState, clearCache: true).ConfigureAwait(false);
                            break;
                        }
                        var failedEvent = descriptor.FailedEventFactory(
                            domainEvent,
                            new InvalidOperationException(currentState.ErrorMessage));
                        if (failedEvent is not null)
                            await PublishProjectionEventAsync(failedEvent, CancellationToken.None).ConfigureAwait(false);
                        currentState = currentState with
                        {
                            Stage = EventProjectorStageType.Completed,
                            Outcome = EventProjectorOutcomeType.Failed,
                            UpdatedTimestamp = DateTime.UtcNow
                        };
                        await PersistLegacyStateAsync(currentState, clearCache: true).ConfigureAwait(false);
                        break;

                    default:
                        throw new InvalidOperationException(
                            $"Invalid stage {currentState.Stage} for event {domainEvent.EventId}.");
                }
            }
        }
        catch (Exception ex)
        {
            currentState = currentState with
            {
                Outcome = EventProjectorOutcomeType.Retrying,
                ErrorMessage = ex.Message,
                UpdatedTimestamp = DateTime.UtcNow
            };
            await PersistLegacyStateAsync(currentState).ConfigureAwait(false);
            throw;
        }
    }

    async Task PersistLegacyStateAsync(EventProjectorStateReadModel state, bool clearCache = false)
    {
        await DbEventSource.InsertEventProjectorStateAsync(state).ConfigureAwait(false);
        if (clearCache)
            BlackboardService.EventSourcing.EventProjectorState.Clear(state.EventId, ProjectorName);
        else
            BlackboardService.EventSourcing.EventProjectorState.Set(state.EventId, ProjectorName, state);
    }

    async ValueTask TerminalizeLegacyUnregisteredAsync(IEvent domainEvent)
    {
        var state = BlackboardService.EventSourcing.EventProjectorState.Get(domainEvent.EventId, ProjectorName)
            ?? await DbEventSource.GetEventProjectorStateAsync(domainEvent.EventId, ProjectorName)
            ?? CreateInitialState(domainEvent.EventId);
        state = state with
        {
            Stage = EventProjectorStageType.Completed,
            Outcome = EventProjectorOutcomeType.Failed,
            ErrorMessage = $"Event type '{domainEvent.GetType().FullName}' is not registered by projector '{ProjectorName}'.",
            UpdatedTimestamp = DateTime.UtcNow
        };
        await PersistLegacyStateAsync(state, clearCache: true).ConfigureAwait(false);
    }

    async Task HandleMaximumAttemptsAsync(IEvent domainEvent)
    {
        if (_reliabilityOptions.FencedExecutionEnabled)
        {
            if (GetDescriptorMap().TryGetValue(domainEvent.GetType(), out var descriptor))
                await ExecutionEngine.HandleMaximumAttemptsAsync(domainEvent, descriptor).ConfigureAwait(false);
            else
                await ExecutionEngine.TerminalizeUnregisteredAsync(domainEvent).ConfigureAwait(false);
            return;
        }

        var state = BlackboardService.EventSourcing.EventProjectorState.Get(domainEvent.EventId, ProjectorName)
            ?? await DbEventSource.GetEventProjectorStateAsync(domainEvent.EventId, ProjectorName)
            ?? CreateInitialState(domainEvent.EventId);
        state = state with
        {
            Stage = EventProjectorStageType.Completed,
            Outcome = EventProjectorOutcomeType.Failed,
            ErrorMessage = $"Maximum {_reliabilityOptions.MaximumReplayAttempts} attempts reached for event {domainEvent.EventId} of type {domainEvent.GetType().Name}.",
            UpdatedTimestamp = DateTime.UtcNow
        };
        await PersistLegacyStateAsync(state, clearCache: true).ConfigureAwait(false);
    }

    async ValueTask<EventProjectorRecoveryResult> RecoverUncompletedEventsAsync(
        IReadOnlyCollection<Type> durableEventTypes,
        CancellationToken cancellationToken)
    {
        var eventNames = durableEventTypes
            .Select(eventType => eventType.Name)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var eventLogs = await DbEventSource.GetUncompletedEventProjectorEventsAsync(
            ProjectorName,
            eventNames,
            cancellationToken) ?? [];

        long queued = 0;
        long terminalFailures = 0;
        foreach (var eventLog in eventLogs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var domainEvent = eventLog.ToDomainEvent();
            if (domainEvent is UnknownEvent)
            {
                var failedState = CreateInitialState(eventLog.EventVersion) with
                {
                    Stage = EventProjectorStageType.Completed,
                    Outcome = EventProjectorOutcomeType.Failed,
                    ErrorMessage = $"Unable to deserialize event '{eventLog.EventName}' from event log version {eventLog.EventVersion}."
                };
                await DbEventSource.InsertEventProjectorStateAsync(
                    failedState,
                    cancellationToken);
                terminalFailures++;
                Logger.LogError(
                    "Unable to recover event {EventId} ({EventName}) for projector {ProjectorName}.",
                    eventLog.EventVersion,
                    eventLog.EventName,
                    ProjectorName);
                continue;
            }

            var currentState = await DbEventSource.GetEventProjectorStateAsync(
                eventLog.EventVersion,
                ProjectorName,
                cancellationToken);
            if (currentState is null)
            {
                Logger.LogWarning(
                    "Skipping event-log recovery for event {EventId} because projector {ProjectorName} has no explicit durable state.",
                    eventLog.EventVersion,
                    ProjectorName);
                continue;
            }
            if (IsTerminal(currentState))
                continue;

            await DbEventSource.InsertEventProjectorStateAsync(
                currentState,
                cancellationToken);
            BlackboardService.EventSourcing.EventProjectorState.Set(
                eventLog.EventVersion,
                ProjectorName,
                currentState);
            await DurableReplayQueue.EnqueueAsync(ProjectorName, domainEvent, cancellationToken).ConfigureAwait(false);
            queued++;
        }

        if (eventLogs.Count > 0)
        {
            Logger.LogInformation(
                "Recovered {EventCount} event-log entries for projector {ProjectorName}.",
                eventLogs.Count,
                ProjectorName);
        }
        return new EventProjectorRecoveryResult(eventLogs.Count, queued, 0, terminalFailures);
    }

    EventProjectorRecoveryCoordinator CreateRecoveryCoordinator()
        => new(
            DbEventSource,
            DurableReplayQueue,
            BlackboardService,
            _reliabilityOptions,
            Logger);

    EventProjectorExecutionEngine ExecutionEngine
    {
        get
        {
            var engine = Volatile.Read(ref _executionEngine);
            if (engine is not null)
                return engine;
            var created = new EventProjectorExecutionEngine(
                DbEventSource,
                _reliabilityOptions,
                ActorName,
                ProjectorName,
                PublishProjectionEventAsync,
                SignalOutbox,
                Logger);
            return Interlocked.CompareExchange(ref _executionEngine, created, null) ?? created;
        }
    }

    EventProjectorOutboxDispatcher OutboxDispatcher
    {
        get
        {
            var dispatcher = Volatile.Read(ref _outboxDispatcher);
            if (dispatcher is not null)
                return dispatcher;
            var created = new EventProjectorOutboxDispatcher(
                DbEventSource,
                _reliabilityOptions,
                ProjectorName,
                PublishProjectionEventAsync,
                Logger);
            return Interlocked.CompareExchange(ref _outboxDispatcher, created, null) ?? created;
        }
    }

    EventProjectorMetricsObserver MetricsObserver
    {
        get
        {
            var observer = Volatile.Read(ref _metricsObserver);
            if (observer is not null)
                return observer;
            var created = new EventProjectorMetricsObserver(
                DbEventSource,
                _reliabilityOptions,
                ProjectorName,
                Logger);
            return Interlocked.CompareExchange(ref _metricsObserver, created, null) ?? created;
        }
    }

    void SignalOutbox()
    {
        if (_reliabilityOptions.TransactionalOutboxEnabled)
            OutboxDispatcher.Signal();
    }

    FrozenDictionary<Type, EventProjectionDescriptor> GetDescriptorMap()
    {
        var current = Volatile.Read(ref _descriptorMap);
        if (current is not null)
            return current;

        lock (_descriptorLock)
        {
            current = _descriptorMap;
            if (current is not null)
                return current;

            var descriptors = ProjectionDescriptors?.ToArray()
                ?? throw new InvalidOperationException($"Projector '{ProjectorName}' returned null descriptors.");
            if (descriptors.Length == 0)
                throw new InvalidOperationException($"Projector '{ProjectorName}' has no projection descriptors.");
            var duplicate = descriptors
                .GroupBy(descriptor => descriptor.SourceEventType)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicate is not null)
                throw new InvalidOperationException(
                    $"Projector '{ProjectorName}' registers '{duplicate.Key.FullName}' more than once.");

            var advertisedTypes = ProjectedEventTypes.ToHashSet();
            var descriptorTypes = descriptors.Select(descriptor => descriptor.SourceEventType).ToHashSet();
            if (!advertisedTypes.SetEquals(descriptorTypes))
                throw new InvalidOperationException(
                    $"Projector '{ProjectorName}' event types do not match its immutable descriptors.");

            current = descriptors.ToFrozenDictionary(descriptor => descriptor.SourceEventType);
            Volatile.Write(ref _descriptorMap, current);
            return current;
        }
    }

    async ValueTask PublishProjectionEventAsync(IEvent domainEvent, CancellationToken cancellationToken)
    {
        domainEvent.CheckForEmptyCommandId();
        EventInitHelper.SetProperty(
            domainEvent,
            nameof(IEvent.Subject),
            new ActorSubject(
                ActorType.Event,
                domainEvent.Subject.Name.Replace("Denormalizer", "Event", StringComparison.Ordinal),
                domainEvent.Subject.Verb,
                domainEvent.Subject.EntityId));
        var publisher = EventPublishers.GetOrAdd(domainEvent.GetType(), CreateEventPublisher);
        await publisher(Context, domainEvent, cancellationToken).ConfigureAwait(false);
    }

    static Func<ICommandActorContext, IEvent, CancellationToken, ValueTask> CreateEventPublisher(Type eventType)
    {
        var eventInterface = eventType.GetInterfaces().SingleOrDefault(type =>
            type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEvent<>))
            ?? throw new InvalidOperationException($"Event type '{eventType.FullName}' has no typed IEvent contract.");
        var entityIdType = eventInterface.GetGenericArguments()[0];
        var method = typeof(BaseEventProjector<TActor>)
            .GetMethod(nameof(PublishTypedEventAsync), BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(eventType, entityIdType);
        return (Func<ICommandActorContext, IEvent, CancellationToken, ValueTask>)method.CreateDelegate(
            typeof(Func<ICommandActorContext, IEvent, CancellationToken, ValueTask>));
    }

    static ValueTask PublishTypedEventAsync<TEvent, TEntityId>(
        ICommandActorContext context,
        IEvent domainEvent,
        CancellationToken cancellationToken)
        where TEvent : class, IEvent<TEntityId>
        where TEntityId : IActorEntityId
        => context.SendAsync<TEvent, TEntityId>((TEvent)domainEvent, cancellationToken);

    void SetReadiness(
        bool isReady,
        long recoveryEventsDiscovered,
        long recoveryEventsQueued,
        string failureReason = "")
    {
        Volatile.Write(ref _readiness, new EventProjectorReadinessSnapshot(
            ProjectorName,
            isReady,
            recoveryEventsDiscovered,
            recoveryEventsQueued,
            DateTimeOffset.UtcNow,
            failureReason));
        EventProjectorMetrics.SetReadiness(ProjectorName, isReady);
    }

    EventProjectorStateReadModel CreateInitialState(long eventId)
    {
        var now = DateTime.UtcNow;
        return new EventProjectorStateReadModel(
            eventId: eventId,
            actorName: ActorName,
            projectorName: ProjectorName,
            isReplay: false,
            attemptNumber: 0,
            stage: EventProjectorStageType.PublishProcessingEvent,
            outcome: EventProjectorOutcomeType.Processing,
            createdTimestamp: now,
            updatedTimestamp: now);
    }

    static bool IsTerminal(EventProjectorStateReadModel state)
        => state.Stage == EventProjectorStageType.Completed
            || state.Outcome is EventProjectorOutcomeType.Completed
                or EventProjectorOutcomeType.Failed
                or EventProjectorOutcomeType.Cancelled
                or EventProjectorOutcomeType.Superseded
                or EventProjectorOutcomeType.AlreadyCompleted;

    static bool IsTerminal(EventProjectorExecutionStateReadModel state)
        => state.Stage == EventProjectorStageType.Completed
            || state.Outcome is EventProjectorOutcomeType.Completed
                or EventProjectorOutcomeType.Failed
                or EventProjectorOutcomeType.Cancelled
                or EventProjectorOutcomeType.Superseded
                or EventProjectorOutcomeType.AlreadyCompleted;

}
