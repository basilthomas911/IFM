using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime;

/// <summary>Handles one VX market-price observation for the EOD realtime branch.</summary>
public static class FuturesMarketPriceUpdated
{
    /// <summary>Updates the VX EOD quote projection.</summary>
    public static async ValueTask ExecuteAsync(
        this FuturesMarketPriceUpdatedRealtimeEvent domainEvent,
        IFuturesEodDataRealtimeContext context)
    {
        _ = await VxQuoteMarketPriceUpdated.ExecuteVxQuoteAsync(
            domainEvent, context.MarketDataApi, context.Projector,
            context.StatusConsoleWriter, context.Logger).ConfigureAwait(false);
    }
}