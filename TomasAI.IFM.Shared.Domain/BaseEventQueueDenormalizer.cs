using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventQueue;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.Domain;

/// <summary>
/// Provides a base class for event queue denormalizers, enabling the processing and denormalization of domain events
/// through an asynchronous event queue.
/// </summary>
/// <remarks>This class is designed to handle domain events by appending them to an asynchronous event queue and
/// processing them using a denormalization mechanism. Subclasses must implement the  <see
/// cref="DenormalizeAsync(DomainEvent)"/> method to define the specific denormalization logic.</remarks>
public abstract class BaseEventQueueDenormalizer
{
    IEventProducer EventProducer { get; }
    ConcurrentAsyncEventQueue<DomainEventCollection> EventQueue { get; }
    protected ILogger Logger { get; }

    /// <summary>
    /// base command event denormalizer constructor
    /// </summary>
    /// <param name="eventProducer"></param>
    /// <param name="logger"></param>
    public BaseEventQueueDenormalizer(IEventProducer eventProducer, ILogger logger)
    {
        EventProducer = eventProducer;
        EventQueue = new ConcurrentAsyncEventQueue<DomainEventCollection>(DenormalizeEventsAsync);
        Logger = logger;
        logger.LogInformationEvent(GetType().Name, "successfully initialized event denormalizer");
    }

    /// <summary>
    /// append domain events to event denormalizer action queue
    /// </summary>
    /// <param name="domainEvents"></param>
    /// <returns></returns>
    public async Task ExecuteAsync(DomainEventCollection domainEvents)
    {
        Logger.LogInformationEvent(GetType().Name, $"ExecuteAsync: denormalizing {domainEvents.Count} domain events");
        if (!EventQueue.IsStarted)
            EventQueue.Start();
        EventQueue.EnqueueAndSignal(domainEvents);
        await Task.CompletedTask;
    }

    /// <summary>
    /// denormalize domain events via event denormalizer actions
    /// </summary>
    /// <param name="domainEvents"></param>
    /// <returns></returns>
    public async Task DenormalizeEventsAsync(DomainEventCollection domainEvents)
    {
        try
        {
            foreach (var e in domainEvents)
                await DenormalizeAsync(e);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "DenormalizeEventsAsync: failed to execute event denormalizer {DenormalizerName} due to {ErrorMessage}", GetType().Name, ex.Message);
        }
    }

    /// <summary>
    /// subclass must implement denormalizer action
    /// </summary>
    /// <param name="event"></param>
    /// <returns></returns>
    protected abstract Task<bool> DenormalizeAsync(IEvent @event);

    /// <summary>
    /// execute denormalizer action
    /// </summary>
    /// <param name="e"></param>
    /// <param name="denormalizerAction"></param>
    /// <returns></returns>
    protected async Task<bool> UpdateReadModelAsync(IEvent e, Func<Task> denormalizerAction, bool postDenormalizeEvent = true)
    {
        try
        {
            if (e.CommandId == Guid.Empty)
                throw new InvalidOperationException($"UpdateReadModelAsync: {e.GetType().Name}.CommandId is empty");
            if (postDenormalizeEvent)
                await EventProducer.PostEventAsync(e);
            await denormalizerAction();
            var completedEvent = (e as DomainEvent)?.ToCompletedEvent();
            if (completedEvent is not null)
            {
                EventModelActor.EventInitHelper.SetProperty(completedEvent, nameof(IEvent.CommandId), e.CommandId);
                await EventProducer.PostEventAsync(completedEvent);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "UpdateReadModelAsync: failed to denormalize event {EventName} due to {ErrorMessage}", e.GetType().Name, ex.Message);
            var failedEvent = (e as DomainEvent)?.ToFailedEvent(ex);
            if (failedEvent is not null)
            {
                EventModelActor.EventInitHelper.SetProperty(failedEvent, nameof(IEvent.CommandId), e.CommandId);
                await EventProducer.PostEventAsync(failedEvent);
            }
        }
        return true;
    }

    /// <summary>
    /// only post event with no read model update
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    protected async Task<bool> PostEventAsync(IEvent e)
    {
        try
        {
            await EventProducer.PostEventAsync(e);
        }
        catch(Exception ex)
        {
            Logger.LogError(ex, "PostEventAsync: failed to post event from denormalizer {EventName} due to {ErrorMessage}", e.GetType().Name, ex.Message);
        }
        return true;
    }

}
