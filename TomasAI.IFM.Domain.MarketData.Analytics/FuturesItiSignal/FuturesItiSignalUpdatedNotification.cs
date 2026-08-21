using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal;

/// <summary>Publishes the UI-facing notification for an authoritative ITI projection.</summary>
internal static class FuturesItiSignalUpdatedNotification
{
    static readonly string ServiceId = $"{LogSourceType.FuturesItiSignalEvent}";

    internal static async ValueTask<bool> PublishUpdatedNotificationAsync(
        this FuturesItiSignalGeneratedCompleteEvent source,
        IEventActorContext context,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        if (source.FuturesItiSignal is not { IsValid: true } signal)
        {
            logger.LogWarning(
                "Skipping invalid Futures ITI notification for {EntityId}",
                source.EntityId);
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
                    source.EntityId.Format()),
                Id = Guid.NewGuid(),
                EntityId = source.EntityId,
                EventId = source.EventId,
                CommandId = source.CommandId,
                AggregateId = source.AggregateId ?? string.Empty,
                EventSource = nameof(FuturesItiSignalGeneratedCompleteEvent),
                ReceivedOn = DateTime.UtcNow,
                FuturesItiSignal = signal,
                SourceEventId = source.Id
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
                source.EntityId);
            return false;
        }
    }
}
