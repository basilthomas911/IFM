using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.StatusConsole;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1027", Justification = "Historical log categories are individual persisted enum values, not bit flags; preserve all numeric identities.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1700", Justification = "Historical log categories are individual persisted enum values, not bit flags; preserve all numeric identities.")]
public enum LogSourceType
{
    System,
    Command,
    Query,
    Event,
    MarketDataFeed,
    MarketDataFeedEvent,
    OptionPricer,
    TWSMarketDataApi = 8,
    IFMApp,
    MarketData,
    DatabaseBackup,
    Denormlaizer,
    TradeOrder,
    PostCommand,
    EndOfDay,
    Fund,
    Trade,
    TradePosition,
    CommandValidation,
    TestSource,
    OrderExecution,
    Reference,
    MarketDataAnalytics,
    TradePlacement,
    // Reserved for historical log-source identities; predictive model runtime is retired.
    ReservedPredictiveModel,
    FuturesBarDataEvent,
    FuturesEodDataEvent,
    FuturesOptionTickDataEvent,
    FuturesTickDataEvent,
    FuturesItiSignalEvent,
    FuturesRsiSignalEvent,
    FuturesTdiSignalEvent,
    FuturesMacdSignalEvent,
    FuturesAtrSignalEvent,
    FuturesAdxSignalEvent,
    FuturesTradeSignalEvent,
    SpreadDistributionJobEvent,
    OptionTradeEvent,
    FuturesOptionPositionMonitorEvent,
    FuturesTickTradeDataChanged,
    FuturesTickQuoteDataChanged,
    FuturesTickTradeDataInserted,
    YieldCurveRatesImported,
    EconomicCalendarsImported,
    FuturesTickQuoteDataInserted,
    FuturesMarketPriceUpdated,
    ApplicationStartup,
    ApplicationShutdown
}
