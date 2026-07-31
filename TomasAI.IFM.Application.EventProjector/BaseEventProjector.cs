using Microsoft.Extensions.Logging;
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
    ILogger logger): IEventProjector<TActor>
    where TActor : ICommandActor<TActor>
{
    static readonly TimeSpan DefaultReplayInterval = TimeSpan.FromSeconds(30);
    ICommandActorContext? _context;

    public abstract string ActorName { get; }
    public abstract string ProjectorName { get; }
    public abstract string DurableProcessQueueName { get; }
    public abstract string DurableReplayQueueName { get; }
    public abstract IReadOnlyCollection<Type> ProjectedEventTypes { get; }

     /// <inheritdoc />
    public abstract ValueTask ProcessDomainEventAsync(IEvent domainEvent);

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
    /// Creates a new instance of the <see cref="EventProjectorBuilder"/> class for configuring event projections.
    /// </summary>
    /// <returns></returns>
    protected EventProjectorBuilder CreateProjectionBuilder() 
        => new (this);

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
        await DurableReplayQueue.DequeueAsync(
            ProjectorName,
            ProcessQueuedDomainEventAsync,
            cancellationToken);
        await RecoverUncompletedEventsAsync(cancellationToken);
        await DurableReplayQueue.StartAsync(ProjectorName, DefaultReplayInterval, cancellationToken);
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
        => await DurableReplayQueue.StopAsync(ProjectorName, cancellationToken);

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
            var projectionState = CreateInitialState(domainEvent.EventId);

            // Persist the recoverable marker before publishing. If publication fails after the event-log
            // transaction committed, startup recovery will find this state and enqueue the source event again.
            await DbEventSource.InsertEventProjectorStateAsync(projectionState);
            BlackboardService.EventProjectorState.Set(domainEvent.EventId, ProjectorName, projectionState);
            DurableReplayQueue.Enqueue(ProjectorName, domainEvent);
        }
    }

    async Task ProcessQueuedDomainEventAsync(IEvent domainEvent)
    {
        var currentState = BlackboardService.EventProjectorState.Get(domainEvent.EventId, ProjectorName)
            ?? await DbEventSource.GetEventProjectorStateAsync(domainEvent.EventId, ProjectorName);

        if (currentState is null)
        {
            currentState = CreateInitialState(domainEvent.EventId);
            await DbEventSource.InsertEventProjectorStateAsync(currentState);
        }

        if (IsTerminal(currentState))
            return;

        BlackboardService.EventProjectorState.Set(domainEvent.EventId, ProjectorName, currentState);
        await ProcessDomainEventAsync(domainEvent);
    }

    async ValueTask RecoverUncompletedEventsAsync(CancellationToken cancellationToken)
    {
        var eventNames = ProjectedEventTypes
            .Select(eventType => eventType.Name)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var eventLogs = await DbEventSource.GetUncompletedEventProjectorEventsAsync(
            ProjectorName,
            eventNames) ?? [];

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
                await DbEventSource.InsertEventProjectorStateAsync(failedState);
                Logger.LogError(
                    "Unable to recover event {EventId} ({EventName}) for projector {ProjectorName}.",
                    eventLog.EventVersion,
                    eventLog.EventName,
                    ProjectorName);
                continue;
            }

            var currentState = await DbEventSource.GetEventProjectorStateAsync(
                eventLog.EventVersion,
                ProjectorName);
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

            await DbEventSource.InsertEventProjectorStateAsync(currentState);
            BlackboardService.EventProjectorState.Set(eventLog.EventVersion, ProjectorName, currentState);
            DurableReplayQueue.Enqueue(ProjectorName, domainEvent, cancellationToken);
        }

        if (eventLogs.Count > 0)
        {
            Logger.LogInformation(
                "Recovered {EventCount} event-log entries for projector {ProjectorName}.",
                eventLogs.Count,
                ProjectorName);
        }
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

    /// <summary>
    /// Posts an event to the command actor context for processing.
    /// </summary>
    /// <typeparam name="TEvent"></typeparam>
    /// <typeparam name="TEntityId"></typeparam>
    /// <param name="e"></param>
    /// <returns></returns>
    protected async ValueTask<bool> PostEventAsync<TEvent, TEntityId>(TEvent e)
        where TEvent : class, IEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        e.CheckForEmptyCommandId();
        EventInitHelper.SetProperty(e, nameof(IEvent.Subject), new ActorSubject(ActorType.Event,
            e.Subject.Name.Replace("Denormalizer", "Event"),
            e.Subject.Verb,
            e.EntityId.Format()));
        await Context.SendAsync<TEvent, TEntityId>(e);
        return true;
    }

    /// <summary>
    /// Logs an exception that occurred during event projection and updates the event projector state accordingly.
    /// </summary>
    /// <param name="ex"></param>
    /// <param name="domainEvent"></param>
    /// <returns></returns>
    protected async Task LogExceptionAsync(Exception ex, IEvent domainEvent)
    {
        var currentState = BlackboardService.EventProjectorState.Get(domainEvent.EventId, ProjectorName)
            ?? await DbEventSource.GetEventProjectorStateAsync(domainEvent.EventId, ProjectorName)
            ?? CreateInitialState(domainEvent.EventId);
        currentState = currentState with
        {
            Outcome = EventProjectorOutcomeType.Failed,
            ErrorMessage = ex.Message
        };
        BlackboardService.EventProjectorState.Set(domainEvent.EventId, ProjectorName, currentState);
        await DbEventSource.InsertEventProjectorStateAsync(currentState);
    }
}
