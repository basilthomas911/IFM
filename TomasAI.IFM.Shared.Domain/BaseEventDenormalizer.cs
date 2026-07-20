using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.Domain;

/// <summary>
/// Provides a base class for implementing event denormalizers that process domain events and update read models or post
/// events as needed.
/// </summary>
/// <remarks>This abstract class defines the core functionality for handling domain events, including executing
/// denormalizer actions, updating read models, and posting events. Subclasses must implement the <see
/// cref="DenormalizeAsync(DomainEvent)"/> method to define the specific denormalization logic for individual
/// events.</remarks>
/// <param name="eventProducer"></param>
/// <param name="logger"></param>
public abstract class BaseEventDenormalizer(IEventProducer eventProducer, ILogger logger)
{
    IEventProducer EventProducer { get; } = eventProducer;
    protected ILogger Logger { get; } = logger;

    /// <summary>
    /// execute denormalizer action on domain events
    /// </summary>
    /// <param name="domainEvents"></param>
    /// <returns></returns>
    public async Task ExecuteAsync(DomainEventCollection domainEvents)
    {
        try
        {
            foreach (var e in domainEvents)
                await DenormalizeAsync(e);
        }
        catch(Exception ex)
        {
            Logger.LogError(ex, $"ExecuteAsync: failed to execute event denormalizer {GetType().Name} due to {ex.Message}");
        }
    }

    /// <summary>
    /// subclass must implement denormalizer action
    /// </summary>
    /// <param name="event"></param>
    /// <returns></returns>
    protected abstract Task<bool> DenormalizeAsync(IEvent @event);

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
            Logger.LogError(ex, $"DenormalizeEventsAsync: failed to execute event denormalizer {GetType().Name} due to {ex.Message}");
        }
    }

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
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"UpdateReadModelAsync: failed to denormalize event {e.GetType().Name} due to {ex.Message}");
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
            Logger.LogError(ex, $"PostEventAsync: failed to post event from denormalizer {e.GetType().Name} due to {ex.Message}");
        }
        return true;
    }

}
