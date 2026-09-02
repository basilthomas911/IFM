using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime;

/// <summary>Handles normalized futures trades routed to the trade-session bar signal.</summary>
public static class FuturesMarketPriceUpdated
{
    /// <summary>Accumulates one trade and forwards every newly completed bar to the Command actor.</summary>
    public static async ValueTask<bool> ExecuteAsync(
        this FuturesMarketPriceUpdatedRealtimeEvent @event,
        IFuturesTradeSessionBarSignalRealtimeContext context,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);
        var accumulatorId = new FuturesTradeSessionBarAccumulatorEntityId(@event.EntityId.ValueDate);
        foreach (var bar in context.Accumulators.Get(accumulatorId).Accept(@event))
            _ = await context.PublishFuturesTradeSessionBarAsync(bar).ConfigureAwait(false);
        return true;
    }
}
