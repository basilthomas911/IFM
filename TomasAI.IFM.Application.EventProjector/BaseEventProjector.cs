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
/// The base implementation creates a new execution context for every domain event in a collection
/// and processes the events sequentially. Enumeration stops as soon as a projection returns an
/// unsuccessful service result.
/// </remarks>
public abstract class BaseEventProjector<TActor> (
    IDurableReplayQueue durableReplayQueue,
    IEventSourceActorDbContext dbEventSource,
    IBlackboardService blackboardService,
    ICommandActorContext commandActorContext,
    ILogger logger): IEventProjector<TActor>
    where TActor : ICommandActor<TActor>
{
    public abstract string ActorName { get; }
    public abstract string ProjectorName { get; }
    public abstract string DurableProcessQueueName { get; }
    public abstract string DurableReplayQueueName { get; }

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

    public ICommandActorContext Context { get; init; } = IsArgumentNull.Set(commandActorContext);

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
    /// Projects a collection of domain events by processing each event sequentially.
    /// </summary>
    /// <param name="domainEvents"></param>
    /// <returns></returns>
    public async ValueTask DomainEventsProjectionAsync(DomainEventCollection domainEvents)
    {
        foreach (var domainEvent in domainEvents)
        {
            var projectionState = new EventProjectorStateReadModel(
                eventId: domainEvent.EventId,
                actorName: ActorName,
                projectorName: ProjectorName,
                isReplay: false,
                attemptNumber: 0,
                stage: EventProjectorStageType.PublishProcessingEvent,
                outcome: EventProjectorOutcomeType.Processing
            );
            BlackboardService.EventProjectorState.Set(domainEvent.EventId, projectionState);
            DurableReplayQueue.Enqueue(DurableReplayQueueName, domainEvent);
        }
    }

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
        var currentState = BlackboardService.EventProjectorState.Get(domainEvent.EventId);
        currentState = currentState with
        {
            Outcome = EventProjectorOutcomeType.Failed,
            ErrorMessage = ex.Message
        };
        BlackboardService.EventProjectorState.Set(domainEvent.EventId, currentState);
        await DbEventSource.InsertEventProjectorStateAsync(currentState);
    }
}
