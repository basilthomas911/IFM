using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarPublisher.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarPublisher.Realtime.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarPublisher.Realtime;

/// <summary>Handles normalized futures trades routed to the trade-session bar publisher.</summary>
public static class FuturesMarketPriceUpdated
{
    /// <summary>Accumulates one trade and forwards every newly completed bar to the Command actor.</summary>
    public static async ValueTask<bool> ExecuteAsync(
        this FuturesMarketPriceUpdatedRealtimeEvent @event,
        IFuturesTradeSessionBarPublisherRealtimeContext context,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);
        foreach (var bar in context.Accumulator.Accept(@event))
            _ = await context.PublishFuturesTradeSessionBarAsync(bar).ConfigureAwait(false);
        return true;
    }
}
