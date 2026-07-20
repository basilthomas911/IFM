using TomasAI.IFM.Shared.MarketDataFeed;

namespace TomasAI.IFM.MarketDataFeed.InteractiveBrokers
{
    public record TickQuoteMessage(
        int RequestId,
        QuoteData TickMarketData)
    {
    }
}
