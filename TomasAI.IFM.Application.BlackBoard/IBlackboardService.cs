namespace TomasAI.IFM.Application.Blackboard;

public interface IBlackboardService
{
    IApplicationBlackboard Application { get; }
    IEventSourcingBlackboard EventSourcing { get; }
    IFundBlackboard Fund { get; }
    IMarketDataBlackboard MarketData { get; }
    IMarketDataAnalyticsBlackboard MarketDataAnalytics { get; }
    IMarketDataFeedBlackboard MarketDataFeed { get; }
    IMarketDataSecuritiesBlackboard MarketDataSecurities { get; }
    IReferenceBlackboard Reference { get; }
    ITradeBlackboard Trade { get; }

    [Obsolete("Use MarketDataSecurities.DatabentoContractMapping.")]
    IDatabentoContractMappingCache DatabentoContractMapping { get; }
    [Obsolete("Use Trade.OptionTrade.")]
    OptionTradeModel OptionTrade { get; }
    [Obsolete("Use Reference.ReferenceLookup.")]
    ReferenceLookupModel ReferenceLookup { get; }
    [Obsolete("Use Trade.TradePositionAction.")]
    TradePositionActionModel TradePositionAction { get; }
    [Obsolete("Use Trade.TradePlanForwardLossLimit.")]
    TradePlanForwardLossLimitModel TradePlanForwardLossLimit { get; }
    [Obsolete("Use Trade.HedgePositionTradeId.")]
    HedgePositionTradeIdModel HedgePositionTradeId { get; }
    [Obsolete("Use MarketDataFeed.FuturesTickData.")]
    FuturesTickDataModel FuturesTickData { get; }
    [Obsolete("Use MarketDataFeed.FuturesOptionTickData.")]
    FuturesOptionTickDataModel FuturesOptionTickData { get; }
    [Obsolete("Use MarketDataFeed.FuturesOptionTickPriceData.")]
    FuturesOptionTickDataModel FuturesOptionTickPriceData { get; }
    [Obsolete("Use MarketDataFeed.FuturesTickDataStreamingParameter.")]
    FuturesTickDataStreamingParameterModel FuturesTickDataStreamingParameter { get; }
    [Obsolete("Use MarketDataFeed.FuturesOptionTickDataStreamingParameter.")]
    FuturesOptionTickDataStreamingParameterModel FuturesOptionTickDataStreamingParameter { get; }
    [Obsolete("Use MarketDataFeed.FuturesEodData.")]
    FuturesEodDataModel FuturesEodData { get; }
    [Obsolete("Use MarketDataFeed.VixFuturesEodData.")]
    VixFuturesEodDataModel VixFuturesEodData { get; }
    [Obsolete("Use MarketDataFeed.FuturesEodDataRange.")]
    FuturesEodDataRangeModel FuturesEodDataRange { get; }
    [Obsolete("Use MarketDataFeed.NormalCurveTable.")]
    NormalCurveTableModel NormalCurveTable { get; }
    [Obsolete("Use MarketDataSecurities.FuturesContract.")]
    FuturesContractModel FuturesContract { get; }
    [Obsolete("Use MarketDataFeed.VixFuturesContractId.")]
    VixFuturesContractIdModel VixFuturesContractId { get; }
    [Obsolete("Use Trade.TradeOrder.")]
    TradeOrderModel TradeOrder { get; }
    [Obsolete("Use EventSourcing.DomainEvents.")]
    DomainEventsModel DomainEvents { get; }
    [Obsolete("Use Trade.IronCondorMDILimit.")]
    IronCondorMDILimitModel IronCondorMDILimit { get; }
    [Obsolete("Use MarketDataSecurities.FuturesContractSymbol.")]
    FuturesContractSymbolModel FuturesContractSymbol { get; }
    [Obsolete("Use MarketDataAnalytics.FuturesItiSignalAveragePredictedTrendDelta.")]
    FuturesItiSignalAveragePredictedTrendDeltaModel
        FuturesItiSignalAveragePredictedTrendDelta { get; }
    [Obsolete("Use MarketDataAnalytics.FuturesItiSignalAveragePredictedTrendDeltaRange.")]
    FuturesItiSignalAveragePredictedTrendDeltaRangeModel
        FuturesItiSignalAveragePredictedTrendDeltaRange { get; }
    [Obsolete("Use MarketDataAnalytics.FuturesItiSignalMDI.")]
    FuturesItiSignalMDIModel FuturesItiSignalMDI { get; }
    [Obsolete("Use MarketDataFeed.FuturesOptionQuote.")]
    FuturesOptionQuoteModel FuturesOptionQuote { get; }
    [Obsolete("Use MarketDataFeed.FuturesOptionQuoteData.")]
    FuturesOptionQuoteDataModel FuturesOptionQuoteData { get; }
    [Obsolete("Use Trade.ForwardLossRatioMap.")]
    ForwardLossRatioMapModel ForwardLossRatioMap { get; }
    [Obsolete("Use Trade.StopLossLimit.")]
    StopLossLimitModel StopLossLimit { get; }
    [Obsolete("Use Trade.SignalProcessor.")]
    SignalProcessorModel SignalProcessor { get; }
    [Obsolete("Use Fund.FundBalance.")]
    FundBalanceModel FundBalance { get; }
    [Obsolete("Use EventSourcing.EventStreamId.")]
    EventStreamIdModel EventStreamId { get; }
    [Obsolete("Use EventSourcing.EventNameId.")]
    EventNameIdModel EventNameId { get; }
    [Obsolete("Use MarketDataFeed.FuturesOpenPrice.")]
    FuturesOpenPriceModel FuturesOpenPrice { get; }
    [Obsolete("Use MarketDataFeed.VixFuturesOpenPrice.")]
    VixFuturesOpenPriceModel VixFuturesOpenPrice { get; }
    [Obsolete("Use MarketDataFeed.StreamingRequestId.")]
    StreamingRequestIdModel StreamingRequestId { get; }
    [Obsolete("Use Application.SequenceCounter.")]
    SequenceCounterModel SequenceCounter { get; }
    [Obsolete("Use MarketData.RiskFreeRate.")]
    RiskFreeRateModel RiskFreeRate { get; }
    [Obsolete("Use MarketDataAnalytics.FuturesRsiSignal.")]
    FuturesRsiSignalModel FuturesRsiSignal { get; }
    [Obsolete("Use MarketDataAnalytics.FuturesRsiDailySignal.")]
    FuturesRsiDailySignalModel FuturesRsiDailySignal { get; }
    [Obsolete("Use EventSourcing.EventProjectorState.")]
    EventProjectorStateModel EventProjectorState { get; }
}
