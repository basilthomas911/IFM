using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Event;

/// <summary>Handles a successfully persisted ITI Hold state transition.</summary>
public static class FuturesItiSignalHoldTradeSetComplete
{
    /// <summary>Publishes the authoritative Hold state to observers.</summary>
    public static async ValueTask<bool> ExecuteAsync(
        this FuturesItiSignalHoldTradeSetCompleteEvent @event,
        IEventActorContext<FuturesItiSignalEventActor> context,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);
        _ = await @event.PublishUpdatedNotificationAsync(context, logger).ConfigureAwait(false);
        return true;
    }
}
