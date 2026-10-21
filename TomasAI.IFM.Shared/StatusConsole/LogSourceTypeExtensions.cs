using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.StatusConsole;

public static class LogSourceTypeExtensions
{
    public static string ToStringFast(this LogSourceType value) => value switch
    {
        LogSourceType.System => nameof(LogSourceType.System),
        LogSourceType.Command => nameof(LogSourceType.Command),
        LogSourceType.Query => nameof(LogSourceType.Query),
        LogSourceType.Event => nameof(LogSourceType.Event),
        LogSourceType.MarketDataFeed => nameof(LogSourceType.MarketDataFeed),
        LogSourceType.MarketDataFeedEvent => nameof(LogSourceType.MarketDataFeedEvent),
        LogSourceType.TWSMarketDataApi => nameof(LogSourceType.TWSMarketDataApi),
        LogSourceType.IFMApp => nameof(LogSourceType.IFMApp),
        LogSourceType.MarketData => nameof(LogSourceType.MarketData),
        LogSourceType.DatabaseBackup => nameof(LogSourceType.DatabaseBackup),
        LogSourceType.Denormlaizer => nameof(LogSourceType.Denormlaizer),
        LogSourceType.TradeOrder => nameof(LogSourceType.TradeOrder),
        LogSourceType.PostCommand => nameof(LogSourceType.PostCommand),
        LogSourceType.EndOfDay => nameof(LogSourceType.EndOfDay),
        LogSourceType.Fund => nameof(LogSourceType.Fund),
        LogSourceType.Trade => nameof(LogSourceType.Trade),
        LogSourceType.TradePosition => nameof(LogSourceType.TradePosition),
        LogSourceType.CommandValidation => nameof(LogSourceType.CommandValidation),
        LogSourceType.TestSource => nameof(LogSourceType.TestSource),
        LogSourceType.OrderExecution => nameof(LogSourceType.OrderExecution),
        LogSourceType.Reference => nameof(LogSourceType.Reference),
        LogSourceType.MarketDataAnalytics => nameof(LogSourceType.MarketDataAnalytics),
        LogSourceType.TradePlacement => nameof(LogSourceType.TradePlacement),
        LogSourceType.ReservedPredictiveModel => nameof(LogSourceType.ReservedPredictiveModel),
        LogSourceType.FuturesBarDataEvent => nameof(LogSourceType.FuturesBarDataEvent),
        LogSourceType.FuturesEodDataEvent => nameof(LogSourceType.FuturesEodDataEvent),
        LogSourceType.FuturesOptionTickDataEvent => nameof(LogSourceType.FuturesOptionTickDataEvent),
        LogSourceType.FuturesTickDataEvent => nameof(LogSourceType.FuturesTickDataEvent),
        LogSourceType.FuturesItiSignalEvent => nameof(LogSourceType.FuturesItiSignalEvent),
        LogSourceType.FuturesRsiSignalEvent => nameof(LogSourceType.FuturesRsiSignalEvent),
        LogSourceType.FuturesTdiSignalEvent => nameof(LogSourceType.FuturesTdiSignalEvent),
        LogSourceType.FuturesMacdSignalEvent => nameof(LogSourceType.FuturesMacdSignalEvent),
        LogSourceType.FuturesAtrSignalEvent => nameof(LogSourceType.FuturesAtrSignalEvent),
        LogSourceType.FuturesAdxSignalEvent => nameof(LogSourceType.FuturesAdxSignalEvent),
        LogSourceType.FuturesTradeSignalEvent => nameof(LogSourceType.FuturesTradeSignalEvent),
        LogSourceType.FuturesTickTradeDataChanged => nameof(LogSourceType.FuturesTickTradeDataChanged),
        LogSourceType.FuturesTickQuoteDataChanged => nameof(LogSourceType.FuturesTickQuoteDataChanged),
        LogSourceType.FuturesTickTradeDataInserted => nameof(LogSourceType.FuturesTickTradeDataInserted),
        LogSourceType.YieldCurveRatesImported => nameof(LogSourceType.YieldCurveRatesImported),
        LogSourceType.EconomicCalendarsImported => nameof(LogSourceType.EconomicCalendarsImported),
        LogSourceType.FuturesTickQuoteDataInserted => nameof(LogSourceType.FuturesTickQuoteDataInserted),
        LogSourceType.FuturesMarketPriceUpdated => nameof(LogSourceType.FuturesMarketPriceUpdated),
        LogSourceType.ApplicationStartup => nameof(LogSourceType.ApplicationStartup),
        LogSourceType.ApplicationShutdown => nameof(LogSourceType.ApplicationShutdown),
        _ => value.ToString()
    };
}
