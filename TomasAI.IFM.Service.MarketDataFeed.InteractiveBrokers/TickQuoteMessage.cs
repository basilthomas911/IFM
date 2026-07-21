using TomasAI.IFM.Shared.MarketDataFeed;

namespace TomasAI.IFM.Service.MarketDataFeed.InteractiveBrokers
{
    public record TickQuoteMessage(
        int RequestId,
        QuoteData TickMarketData)
    {
    }
}
