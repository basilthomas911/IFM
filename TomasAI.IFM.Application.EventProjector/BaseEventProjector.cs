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
/// The base implementation owns the durable queue lifecycle for the projector. Startup registers
/// the projection handler before starting the process and replay workers, while event-batch handling
/// records projection state and durably enqueues each event for asynchronous processing.
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
    /// Registers the projector's event handler and starts its durable process and replay queue workers.
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
        _ = GetDescriptorMap();
        SetReadiness(false, 0, 0);
        try
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
            var recovery = _reliabilityOptions.BoundedRecoveryEnabled
                ? await CreateRecoveryCoordinator().RecoverAsync(
                    ActorName,
                    ProjectorName,
                    ProjectedEventTypes,
                    cancellationToken).ConfigureAwait(false)
                : await RecoverUncompletedEventsAsync(cancellationToken).ConfigureAwait(false);
            await DurableReplayQueue.StartAsync(
                ProjectorName,
                _reliabilityOptions.InitialReplayDelay,
                cancellationToken).ConfigureAwait(false);
            SetReadiness(true, recovery.Discovered, recovery.Queued);
        }
        catch (Exception ex)
        {
            try
            {
                await DurableReplayQueue.StopAsync(ProjectorName, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception stopException)
            {
                Logger.LogWarning(stopException, "Unable to roll back projector queue startup for {ProjectorName}.", ProjectorName);
            }
            _context = null;
            SetReadiness(false, 0, 0, ex.Message);
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
        await DurableReplayQueue.StopAsync(ProjectorName, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Publishes a collection of domain events to the projector's durable process queue.
    /// </summary>
    /// <param name="domainEvents">The domain events to enqueue for asynchronous projection.</param>
    /// <returns>A completed task-like value after all events have been durably enqueued.</returns>
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

    async Task ProcessQueuedDomainEventAsync(IEvent domainEvent)
    {
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
                        if (descriptor.PublishProcessingEvent)
                            await PublishProjectionEventAsync(domainEvent, CancellationToken.None).ConfigureAwait(false);
                        currentState = currentState with
                        {
                            Stage = EventProjectorStageType.ApplyProjection,
                            UpdatedTimestamp = DateTime.UtcNow
                        };
                        await PersistLegacyStateAsync(currentState).ConfigureAwait(false);
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
                        currentState = currentState with
                        {
                            Stage = applyResult.Success
                                ? EventProjectorStageType.PublishCompletedEvent
                                : EventProjectorStageType.PublishFailedEvent,
                            Outcome = applyResult.Success
                                ? EventProjectorOutcomeType.Processing
                                : EventProjectorOutcomeType.Retrying,
                            ErrorMessage = applyResult.ErrorMessage,
                            UpdatedTimestamp = DateTime.UtcNow
                        };
                        await PersistLegacyStateAsync(currentState).ConfigureAwait(false);
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
            await ExecutionEngine.HandleMaximumAttemptsAsync(domainEvent).ConfigureAwait(false);
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

    async ValueTask<EventProjectorRecoveryResult> RecoverUncompletedEventsAsync(CancellationToken cancellationToken)
    {
        var eventNames = ProjectedEventTypes
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
                Logger);
            return Interlocked.CompareExchange(ref _executionEngine, created, null) ?? created;
        }
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
        => Volatile.Write(ref _readiness, new EventProjectorReadinessSnapshot(
            ProjectorName,
            isReady,
            recoveryEventsDiscovered,
            recoveryEventsQueued,
            DateTimeOffset.UtcNow,
            failureReason));

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
