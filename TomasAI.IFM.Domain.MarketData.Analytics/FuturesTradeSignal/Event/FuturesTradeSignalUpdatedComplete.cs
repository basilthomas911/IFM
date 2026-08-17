using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Event;

public static class FuturesTradeSignalUpdatedComplete
{
    static FuturesTradeSignalUpdatedComplete()
    {
        ServiceId = $"{LogSourceType.FuturesTradeSignalEvent}";
    }
    static string ServiceId { get; } = default!;

    /// <summary>
    /// Handles the completion of a trade signal updated event.
    /// </summary>
    public static async ValueTask<bool> ExecuteAsync(this FuturesTradeSignalUpdatedCompleteEvent e, IEventActorContext context, IStatusConsoleWriter statusConsoleWriter, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(statusConsoleWriter);
        ArgumentNullException.ThrowIfNull(logger);

        try
        {
            var tradeSignal = e.FuturesTradeSignal
                ?? throw new InvalidOperationException("FuturesTradeSignal payload is required.");
            var notification = new FuturesTradeSignalUpdatedNotifyEvent
            {
                Subject = new ActorSubject(
                    ActorType.Notify,
                    FuturesTradeSignalUpdatedNotifyEvent.Actor,
                    FuturesTradeSignalUpdatedNotifyEvent.Verb,
                    e.EntityId.Format()),
                Id = Guid.NewGuid(),
                EntityId = e.EntityId,
                EventId = e.EventId,
                CommandId = e.CommandId,
                AggregateId = e.AggregateId ?? string.Empty,
                EventSource = nameof(FuturesTradeSignalUpdatedCompleteEvent),
                ReceivedOn = DateTime.UtcNow,
                FuturesTradeSignal = tradeSignal
            };
            await context.SendAsync<FuturesTradeSignalUpdatedNotifyEvent, FuturesTradeSignalEntityId>(
                notification).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            // The durable projection has already completed. Notify publication is observational
            // and must never reverse, fail, or retry the persisted update.
            logger.LogErrorEvent(
                ServiceId,
                ex,
                "Unable to publish futures trade-signal notification for {EntityId}",
                e.EntityId);
        }
        return false;
    }

}
