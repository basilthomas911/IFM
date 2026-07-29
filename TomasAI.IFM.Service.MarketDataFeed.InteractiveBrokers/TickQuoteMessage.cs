using TomasAI.IFM.Domain.MarketData.Feed.Shared;

namespace TomasAI.IFM.Service.MarketDataFeed.InteractiveBrokers
{
    public record TickQuoteMessage(
        int RequestId,
        QuoteData TickMarketData)
    {
    }
}
