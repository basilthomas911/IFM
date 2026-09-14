using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal;

/// <summary>Publishes the UI-facing notification for an authoritative ITI projection.</summary>
internal static class FuturesItiSignalUpdatedNotification
{
    static readonly string ServiceId = $"{LogSourceType.FuturesItiSignalEvent}";

    internal static async ValueTask<bool> PublishUpdatedNotificationAsync<TActor>(
        this FuturesItiSignalGeneratedCompleteEvent source,
        IEventActorContext<TActor> context,
        ILogger logger)
        where TActor : IActor
        => await PublishAsync(
            source.EntityId,
            source.Id,
            source.EventId,
            source.CommandId,
            source.AggregateId,
            source.FuturesItiSignal,
            source.EventName,
            context,
            logger).ConfigureAwait(false);

    /// <summary>Publishes a notification after the Hold state has been persisted.</summary>
    internal static async ValueTask<bool> PublishUpdatedNotificationAsync<TActor>(
        this FuturesItiSignalHoldTradeSetCompleteEvent source,
        IEventActorContext<TActor> context,
        ILogger logger)
        where TActor : IActor
        => await PublishAsync(
            source.EntityId,
            source.Id,
            source.EventId,
            source.CommandId,
            source.AggregateId,
            source.FuturesItiSignal,
            source.EventName,
            context,
            logger).ConfigureAwait(false);

    /// <summary>Publishes a notification after the Ready state has been persisted.</summary>
    internal static async ValueTask<bool> PublishUpdatedNotificationAsync<TActor>(
        this FuturesItiSignalHoldTradeClearedCompleteEvent source,
        IEventActorContext<TActor> context,
        ILogger logger)
        where TActor : IActor
        => await PublishAsync(
            source.EntityId,
            source.Id,
            source.EventId,
            source.CommandId,
            source.AggregateId,
            source.FuturesItiSignal,
            source.EventName,
            context,
            logger).ConfigureAwait(false);

    static async ValueTask<bool> PublishAsync<TActor>(
        FuturesItiSignalEntityId entityId,
        Guid sourceEventId,
        long eventId,
        Guid commandId,
        string aggregateId,
        FuturesItiSignalV2ReadModel? signal,
        string eventSource,
        IEventActorContext<TActor> context,
        ILogger logger)
        where TActor : IActor
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        if (signal is not { IsValid: true })
        {
            logger.LogWarning(
                "Skipping invalid Futures ITI notification for {EntityId}",
                entityId);
            return false;
        }

        try
        {
            var notification = new FuturesItiSignalUpdatedNotifyEvent
            {
                Subject = new ActorSubject(
                    ActorType.Notify,
                    FuturesItiSignalUpdatedNotifyEvent.Actor,
                    FuturesItiSignalUpdatedNotifyEvent.Verb,
                    entityId.Format()),
                Id = Guid.NewGuid(),
                EntityId = entityId,
                EventId = eventId,
                CommandId = commandId,
                AggregateId = aggregateId ?? string.Empty,
                EventSource = eventSource,
                ReceivedOn = DateTime.UtcNow,
                FuturesItiSignal = signal,
                SourceEventId = sourceEventId
            };

            await context.SendAsync<FuturesItiSignalUpdatedNotifyEvent, FuturesItiSignalEntityId>(
                notification).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception)
        {
            // Projection has already succeeded. Observational delivery must not reverse
            // or retry the authoritative ITI write.
            logger.LogErrorEvent(
                ServiceId,
                exception,
                "Unable to publish Futures ITI notification for {EntityId}",
                entityId);
            return false;
        }
    }
}
