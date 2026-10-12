using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;

namespace TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;

public enum TickAggregationProcessingStage
{
    Idle = 0,
    Starting = 1,
    ValueDateFlush = 2,
    StatisticsReplayPublish = 3,
    TradeReplayPublish = 4,
    QuoteUpdate = 5,
    QuoteMarketPricePublish = 6,
    QuoteLiveRoute = 7,
    QuoteFlush = 8,
    TradeUpdate = 9,
    TradeMarketPricePublish = 10,
    TradeLiveRoute = 11,
    TradeQuoteFlush = 12,
    TradePublish = 13,
    StatisticsUpdate = 14,
    StatisticsPublish = 15
}
