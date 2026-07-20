namespace TomasAI.IFM.Application.Blackboard;

public interface IBlackboardService
{
    OptionTradeModel OptionTrade {get;}
    ReferenceLookupModel ReferenceLookup { get; }
    TradePositionActionModel TradePositionAction { get; }
    TradePlanForwardLossLimitModel TradePlanForwardLossLimit { get; }
    HedgePositionTradeIdModel HedgePositionTradeId { get; }
    FuturesTickDataModel FuturesTickData { get; }
    FuturesOptionTickDataModel FuturesOptionTickData { get; }
    FuturesOptionTickDataModel FuturesOptionTickPriceData { get; }
    FuturesTickDataStreamingParameterModel FuturesTickDataStreamingParameter { get; }
    StreamingRequestIdModel FuturesOptionTickDataStreamingParameter { get; }
    FuturesEodDataModel FuturesEodData { get; }
    VixFuturesEodDataModel VixFuturesEodData { get; }
    FuturesEodDataRangeModel FuturesEodDataRange { get; }
    NormalCurveTableModel NormalCurveTable { get; }
    FuturesContractModel FuturesContract { get; }
    VixFuturesContractIdModel VixFuturesContractId { get; }
    TradeOrderModel TradeOrder { get; }
    DomainEventsModel DomainEvents { get; }
    IronCondorMDILimitModel IronCondorMDILimit { get; }
    FuturesContractSymbolModel FuturesContractSymbol { get; }
    FuturesItiSignalAveragePredictedTrendDeltaModel FuturesItiSignalAveragePredictedTrendDelta { get; }
    FuturesItiSignalAveragePredictedTrendDeltaRangeModel FuturesItiSignalAveragePredictedTrendDeltaRange { get; }
    FuturesItiSignalMDIModel FuturesItiSignalMDI { get; }
    FuturesOptionQuoteModel FuturesOptionQuote { get; }
    FuturesOptionQuoteDataModel FuturesOptionQuoteData { get; }
    ForwardLossRatioMapModel ForwardLossRatioMap { get; }
    StopLossLimitModel StopLossLimit { get; }
    SignalProcessorModel SignalProcessor { get; }
    FundBalanceModel FundBalance { get; }
    EventStreamIdModel EventStreamId { get; }
    EventNameIdModel EventNameId { get; }
    FuturesOpenPriceModel FuturesOpenPrice { get; }
    VixFuturesOpenPriceModel VixFuturesOpenPrice { get; }
    StreamingRequestIdModel StreamingRequestId { get; }
    SequenceCounterModel SequenceCounter { get; }
    RiskFreeRateModel RiskFreeRate { get; }
    FuturesRsiSignalModel FuturesRsiSignal { get; }
    FuturesRsiDailySignalModel FuturesRsiDailySignal { get; }
}
