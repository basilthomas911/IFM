namespace TomasAI.IFM.Shared.EventSourcing;

/// <summary>
/// aggregate names
/// </summary>
public enum BoundedContextName
{
    Undefined = 0,
    EconomicCalendarBoundedContext = 1,
    FundBoundedContext = 2,
    FundTransactionBoundedContext = 3,
    FuturesContractBoundedContext = 4,
    FuturesOptionContractBoundedContext = 5,
    YieldCurveRateBoundedContext = 6,
    FuturesBarDataBoundedContext = 7,
    FuturesTickDataBoundedContext = 8,
    FuturesEodDataBoundedContext = 9,
    FuturesOptionTickDataBoundedContext = 10,
    OptionTradeBoundedContext = 12,
    TradePlanBoundedContext = 13,
    LookupTypeBoundedContext = 15,
    SystemAdminBoundedContext = 16,
    MarketDataFeedBoundedContext = 17,
    TradeOrderBoundedContext = 18,
    StrikePriceVolatilityBoundedContext = 19,
    FuturesClosingPriceBoundedContext = 20,
    ApplicationBoundedContext = 21,
    FuturesTradeSignalBoundedContext = 22,
    IronCondorTradeAlgorithmBoundedContext = 23,
    TradePlanForwardLossLimitBoundedContext = 24,
    TradePlacementBoundedContext = 25,
    FuturesRsiSignalBoundedContext = 26,
    FuturesTdiSignalBoundedContext = 27,
    FuturesTradeSignalLLMBoundedContext = 28,
    TelemetryLogsBoundedContext = 29,
    FuturesItiSignalBoundedContext = 30,
    FuturesItiTrendBoundedContext = 31,
    TradeAlgorithmBoundedContext = 32,
    FuturesMacdSignalBoundedContext = 33,
    FuturesAtrSignalBoundedContext = 34,
    FuturesAdxSignalBoundedContext = 35,
    /// <summary>Routes event-sourced EMA signal commands.</summary>
    FuturesEmaSignalBoundedContext = 36,
    /// <summary>Routes event-sourced Bollinger Band signal commands.</summary>
    FuturesBbSignalBoundedContext = 37,
    /// <summary>Routes event-sourced VX front/back term-structure updates.</summary>
    FuturesVxTermStructureSignalBoundedContext = 38,
    /// <summary>Routes event-sourced exact futures-session VWAP updates.</summary>
    FuturesVwapSignalBoundedContext = 39,
    /// <summary>Routes Intrinsic Time Strategy workflow orchestration commands.</summary>
    IntrinsicTimeStrategyWorkflowBoundedContext = 40,
    /// <summary>Routes Regime Discovery strategy pipeline commands.</summary>
    RegimeDiscoveryPipelineBoundedContext = 41,
    /// <summary>Routes Market Condition strategy pipeline commands.</summary>
    MarketConditionPipelineBoundedContext = 42,
    /// <summary>Routes Trade Selection strategy pipeline commands.</summary>
    TradeSelectionPipelineBoundedContext = 43,
    /// <summary>Routes Order Composition strategy pipeline commands.</summary>
    OrderCompositionPipelineBoundedContext = 44,
    /// <summary>Routes Risk Management strategy pipeline commands.</summary>
    RiskManagementPipelineBoundedContext = 45,
    /// <summary>Routes durable publication of session-aligned futures OHLCV bars.</summary>
    FuturesTradeSessionBarSignalBoundedContext = 46,
    /// <summary>Routes event-sourced Market Outlook snapshot accumulation commands.</summary>
    MarketOutlookSnapshotBoundedContext = 47,
    /// <summary>Routes immutable strategy parameter-set lifecycle commands.</summary>
    StrategyConfigurationBoundedContext = 48,
    /// <summary>Routes Portfolio aggregate commands.</summary>
    PortfolioBoundedContext = 49,
    /// <summary>Routes Portfolio-owned Fund aggregate commands.</summary>
    PortfolioFundBoundedContext = 50,
    /// <summary>Routes audited reference catalog creation commands.</summary>
    TradeStrategyFamilyBoundedContext = 51,
    /// <summary>Routes immutable market-data download outcomes.</summary>
    DownloadLogBoundedContext = 52,
    /// <summary>Routes authoritative Portfolio ledger commands and queries.</summary>
    GeneralLedgerBoundedContext = 53,
    /// <summary>Routes Portfolio capacity reservation, consumption and lifecycle operations.</summary>
    CapacityReservationBoundedContext = 54,
    /// <summary>Routes Portfolio-owned atomic order-composition decisions.</summary>
    PortfolioOrderCompositionBoundedContext = 55,
    /// <summary>Routes broker-neutral order-execution lifecycle commands.</summary>
    OrderExecutionBoundedContext = 56,
    /// <summary>Routes durable logical broker-order mutation and evidence commands.</summary>
    BrokerOrderBoundedContext = 57,
    /// <summary>Routes broker account snapshots, gates, holds, and qualification decisions.</summary>
    BrokerAccountBoundedContext = 58,
    /// <summary>Routes established Futures Trade commands.</summary>
    FuturesTradeBoundedContext = 59,
    /// <summary>Routes resident one-leg Futures position commands.</summary>
    FuturesTradePositionBoundedContext = 60,
    /// <summary>Routes Futures Iron Condor position commands.</summary>
    FuturesIronCondorTradePositionBoundedContext = 61,
    /// <summary>Routes Futures Vertical Spread position commands.</summary>
    FuturesVerticalSpreadTradePositionBoundedContext = 62,
    /// <summary>Routes strategy-specific position Trade Plan Function commands.</summary>
    StrategyPositionTradePlanBoundedContext = 63,
    /// <summary>Routes strategy-specific position exit-workflow commands.</summary>
    StrategyPositionExitWorkflowBoundedContext = 64,
    /// <summary>Routes authoritative actor supervision commands and operational queries.</summary>
    SupervisorBoundedContext = 65,
}
