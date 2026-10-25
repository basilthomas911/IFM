using TomasAI.IFM.Shared.MarketData.Diagnostics;

namespace TomasAI.IFM.Shared.MarketData.Diagnostics;

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
