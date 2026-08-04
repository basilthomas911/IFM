namespace TomasAI.IFM.Shared.EventSourcing;

/// <summary>
/// aggregate names
/// </summary>
public enum BoundedContextName
{
    Undefined,
    EconomicCalendarBoundedContext,
    FundBoundedContext,
    FundTransactionBoundedContext,
    FuturesContractBoundedContext,
    FuturesOptionContractBoundedContext,
    YieldCurveRateBoundedContext,
    FuturesBarDataBoundedContext,
    FuturesTickDataBoundedContext,
    FuturesEodDataBoundedContext,
    FuturesOptionTickDataBoundedContext,
    SpreadDistributionBoundedContext,
    OptionTradeBoundedContext,
    TradePlanBoundedContext,
    SpreadDistributionJobBoundedContext,
    LookupTypeBoundedContext,
    SystemAdminBoundedContext,
    MarketDataFeedBoundedContext,
    TradeOrderBoundedContext,
    StrikePriceVolatilityBoundedContext,
    FuturesClosingPriceBoundedContext,
    ApplicationBoundedContext,
    FuturesTradeSignalBoundedContext,
    IronCondorTradeAlgorithmBoundedContext,
    TradePlanForwardLossLimitBoundedContext,
    TradePlacementBoundedContext,
    FuturesRsiSignalBoundedContext,
    FuturesTdiSignalBoundedContext,
    FuturesTradeSignalLLMBoundedContext,
    TelemetryLogsBoundedContext,
    FuturesItiSignalBoundedContext,
    FuturesItiTrendBoundedContext,
    FuturesOptionQuoteDataBoundedContext,
    TradeAlgorithmBoundedContext,
    FuturesMacdSignalBoundedContext,
    FuturesAtrSignalBoundedContext,
}
