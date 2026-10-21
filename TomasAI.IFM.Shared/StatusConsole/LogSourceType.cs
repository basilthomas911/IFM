using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.StatusConsole;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1027", Justification = "Historical log categories are individual persisted enum values, not bit flags; preserve all numeric identities.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1700", Justification = "Historical log categories are individual persisted enum values, not bit flags; preserve all numeric identities.")]
public enum LogSourceType
{
    System = 0,
    Command = 1,
    Query = 2,
    Event = 3,
    MarketDataFeed = 4,
    MarketDataFeedEvent = 5,
    TWSMarketDataApi = 8,
    IFMApp = 9,
    MarketData = 10,
    DatabaseBackup = 11,
    Denormlaizer = 12,
    TradeOrder = 13,
    PostCommand = 14,
    EndOfDay = 15,
    Fund = 16,
    Trade = 17,
    TradePosition = 18,
    CommandValidation = 19,
    TestSource = 20,
    OrderExecution = 21,
    Reference = 22,
    MarketDataAnalytics = 23,
    TradePlacement = 24,
    // Reserved for historical log-source identities; predictive model runtime is retired.
    ReservedPredictiveModel = 25,
    FuturesBarDataEvent = 26,
    FuturesEodDataEvent = 27,
    FuturesOptionTickDataEvent = 28,
    FuturesTickDataEvent = 29,
    FuturesItiSignalEvent = 30,
    FuturesRsiSignalEvent = 31,
    FuturesTdiSignalEvent = 32,
    FuturesMacdSignalEvent = 33,
    FuturesAtrSignalEvent = 34,
    FuturesAdxSignalEvent = 35,
    FuturesTradeSignalEvent = 36,
    OptionTradeEvent = 38,
    FuturesOptionPositionMonitorEvent = 39,
    FuturesTickTradeDataChanged = 40,
    FuturesTickQuoteDataChanged = 41,
    FuturesTickTradeDataInserted = 42,
    YieldCurveRatesImported = 43,
    EconomicCalendarsImported = 44,
    FuturesTickQuoteDataInserted = 45,
    FuturesMarketPriceUpdated = 46,
    ApplicationStartup = 47,
    ApplicationShutdown = 48,
}
