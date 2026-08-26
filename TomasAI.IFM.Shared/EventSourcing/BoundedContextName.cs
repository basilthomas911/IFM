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
    TradeAlgorithmBoundedContext,
    FuturesMacdSignalBoundedContext,
    FuturesAtrSignalBoundedContext,
    FuturesAdxSignalBoundedContext,
    /// <summary>Routes Intrinsic Time Strategy workflow orchestration commands.</summary>
    IntrinsicTimeStrategyWorkflowBoundedContext,
    /// <summary>Routes Regime Discovery strategy pipeline commands.</summary>
    RegimeDiscoveryPipelineBoundedContext,
    /// <summary>Routes Market Condition strategy pipeline commands.</summary>
    MarketConditionPipelineBoundedContext,
    /// <summary>Routes Trade Selection strategy pipeline commands.</summary>
    TradeSelectionPipelineBoundedContext,
    /// <summary>Routes Order Composition strategy pipeline commands.</summary>
    OrderCompositionPipelineBoundedContext,
    /// <summary>Routes Risk Management strategy pipeline commands.</summary>
    RiskManagementPipelineBoundedContext,
    /// <summary>Routes durable publication of session-aligned futures OHLCV bars.</summary>
    FuturesTradeSessionBarPublisherBoundedContext,
    /// <summary>Routes event-sourced Market Outlook snapshot accumulation commands.</summary>
    MarketOutlookSnapshotBoundedContext,
}
