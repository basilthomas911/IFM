using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.StatusConsole;

public enum StatusCodeType
{
    Ok,
    Error
}

public static class StatusCodeTypeExtensions
{
    public static string ToStringFast(this StatusCodeType value) => value switch
    {
        StatusCodeType.Ok => nameof(StatusCodeType.Ok),
        StatusCodeType.Error => nameof(StatusCodeType.Error),
        _ => value.ToString()
    };
}

public enum LogSourceType
{
    System,
    Command,
    Query,
    Event,
    MarketDataFeed,
    MarketDataFeedEvent,
    OptionPricer,
    SignalR,
    TWSMarketDataApi,
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
    PredictiveModel,
    FuturesBarDataEvent,
    FuturesOptionQuoteDataEvent,
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
    FuturesOptionPositionMonitorEvent
}

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
        LogSourceType.OptionPricer => nameof(LogSourceType.OptionPricer),
        LogSourceType.SignalR => nameof(LogSourceType.SignalR),
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
        LogSourceType.PredictiveModel => nameof(LogSourceType.PredictiveModel),
        LogSourceType.FuturesBarDataEvent => nameof(LogSourceType.FuturesBarDataEvent),
        LogSourceType.FuturesOptionQuoteDataEvent => nameof(LogSourceType.FuturesOptionQuoteDataEvent),
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
        _ => value.ToString()
    };
}
