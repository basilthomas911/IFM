using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.Blackboard;

/// <summary>
/// Provides domain-grouped access to application cache models.
/// </summary>
public class BlackboardService : IBlackboardService
{
    public BlackboardService(IRedisCache redisCache, IJsonSerializer jsonSerializer)
    {
        IsArgumentNull.Check(redisCache);
        IsArgumentNull.Check(jsonSerializer);

        Application = new ApplicationBlackboard(redisCache);
        EventSourcing = new EventSourcingBlackboard(redisCache, jsonSerializer);
        Fund = new FundBlackboard(redisCache, jsonSerializer);
        MarketData = new MarketDataBlackboard(redisCache, jsonSerializer);
        MarketDataAnalytics = new MarketDataAnalyticsBlackboard(redisCache, jsonSerializer);
        MarketDataFeed = new MarketDataFeedBlackboard(redisCache, jsonSerializer);
        MarketDataSecurities = new MarketDataSecuritiesBlackboard(
            redisCache,
            jsonSerializer);
        Reference = new ReferenceBlackboard(redisCache, jsonSerializer);
        Trade = new TradeBlackboard(redisCache, jsonSerializer);
    }

    public IApplicationBlackboard Application { get; }
    public IEventSourcingBlackboard EventSourcing { get; }
    public IFundBlackboard Fund { get; }
    public IMarketDataBlackboard MarketData { get; }
    public IMarketDataAnalyticsBlackboard MarketDataAnalytics { get; }
    public IMarketDataFeedBlackboard MarketDataFeed { get; }
    public IMarketDataSecuritiesBlackboard MarketDataSecurities { get; }
    public IReferenceBlackboard Reference { get; }
    public ITradeBlackboard Trade { get; }

    [Obsolete("Use MarketDataSecurities.DatabentoContractMapping.")]
    public IDatabentoContractMappingCache DatabentoContractMapping =>
        MarketDataSecurities.DatabentoContractMapping;
    [Obsolete("Use Trade.OptionTrade.")]
    public OptionTradeModel OptionTrade => Trade.OptionTrade;
    [Obsolete("Use Reference.ReferenceLookup.")]
    public ReferenceLookupModel ReferenceLookup => Reference.ReferenceLookup;
    [Obsolete("Use Trade.TradePositionAction.")]
    public TradePositionActionModel TradePositionAction => Trade.TradePositionAction;
    [Obsolete("Use Trade.TradePlanForwardLossLimit.")]
    public TradePlanForwardLossLimitModel TradePlanForwardLossLimit =>
        Trade.TradePlanForwardLossLimit;
    [Obsolete("Use Trade.HedgePositionTradeId.")]
    public HedgePositionTradeIdModel HedgePositionTradeId => Trade.HedgePositionTradeId;
    [Obsolete("Use MarketDataFeed.FuturesTickData.")]
    public FuturesTickDataModel FuturesTickData => MarketDataFeed.FuturesTickData;
    [Obsolete("Use MarketDataFeed.FuturesOptionTickData.")]
    public FuturesOptionTickDataModel FuturesOptionTickData =>
        MarketDataFeed.FuturesOptionTickData;
    [Obsolete("Use MarketDataFeed.FuturesOptionTickPriceData.")]
    public FuturesOptionTickDataModel FuturesOptionTickPriceData =>
        MarketDataFeed.FuturesOptionTickPriceData;
    [Obsolete("Use MarketDataFeed.FuturesTickDataStreamingParameter.")]
    public FuturesTickDataStreamingParameterModel FuturesTickDataStreamingParameter =>
        MarketDataFeed.FuturesTickDataStreamingParameter;
    [Obsolete("Use MarketDataFeed.FuturesOptionTickDataStreamingParameter.")]
    public FuturesOptionTickDataStreamingParameterModel
        FuturesOptionTickDataStreamingParameter =>
            MarketDataFeed.FuturesOptionTickDataStreamingParameter;
    [Obsolete("Use MarketDataFeed.FuturesEodData.")]
    public FuturesEodDataModel FuturesEodData => MarketDataFeed.FuturesEodData;
    [Obsolete("Use MarketDataFeed.VixFuturesEodData.")]
    public VixFuturesEodDataModel VixFuturesEodData => MarketDataFeed.VixFuturesEodData;
    [Obsolete("Use MarketDataFeed.FuturesEodDataRange.")]
    public FuturesEodDataRangeModel FuturesEodDataRange =>
        MarketDataFeed.FuturesEodDataRange;
    [Obsolete("Use MarketDataFeed.NormalCurveTable.")]
    public NormalCurveTableModel NormalCurveTable => MarketDataFeed.NormalCurveTable;
    [Obsolete("Use MarketDataSecurities.FuturesContract.")]
    public FuturesContractModel FuturesContract => MarketDataSecurities.FuturesContract;
    [Obsolete("Use MarketDataFeed.VixFuturesContractId.")]
    public VixFuturesContractIdModel VixFuturesContractId =>
        MarketDataFeed.VixFuturesContractId;
    [Obsolete("Use Trade.TradeOrder.")]
    public TradeOrderModel TradeOrder => Trade.TradeOrder;
    [Obsolete("Use EventSourcing.DomainEvents.")]
    public DomainEventsModel DomainEvents => EventSourcing.DomainEvents;
    [Obsolete("Use Trade.IronCondorMDILimit.")]
    public IronCondorMDILimitModel IronCondorMDILimit => Trade.IronCondorMDILimit;
    [Obsolete("Use MarketDataSecurities.FuturesContractSymbol.")]
    public FuturesContractSymbolModel FuturesContractSymbol =>
        MarketDataSecurities.FuturesContractSymbol;
    [Obsolete("Use MarketDataAnalytics.FuturesItiSignalAveragePredictedTrendDelta.")]
    public FuturesItiSignalAveragePredictedTrendDeltaModel
        FuturesItiSignalAveragePredictedTrendDelta =>
            MarketDataAnalytics.FuturesItiSignalAveragePredictedTrendDelta;
    [Obsolete("Use MarketDataAnalytics.FuturesItiSignalAveragePredictedTrendDeltaRange.")]
    public FuturesItiSignalAveragePredictedTrendDeltaRangeModel
        FuturesItiSignalAveragePredictedTrendDeltaRange =>
            MarketDataAnalytics.FuturesItiSignalAveragePredictedTrendDeltaRange;
    [Obsolete("Use MarketDataAnalytics.FuturesItiSignalMDI.")]
    public FuturesItiSignalMDIModel FuturesItiSignalMDI =>
        MarketDataAnalytics.FuturesItiSignalMDI;
    [Obsolete("Use MarketDataFeed.FuturesOptionQuote.")]
    public FuturesOptionQuoteModel FuturesOptionQuote => MarketDataFeed.FuturesOptionQuote;
    [Obsolete("Use MarketDataFeed.FuturesOptionQuoteData.")]
    public FuturesOptionQuoteDataModel FuturesOptionQuoteData =>
        MarketDataFeed.FuturesOptionQuoteData;
    [Obsolete("Use Trade.ForwardLossRatioMap.")]
    public ForwardLossRatioMapModel ForwardLossRatioMap => Trade.ForwardLossRatioMap;
    [Obsolete("Use Trade.StopLossLimit.")]
    public StopLossLimitModel StopLossLimit => Trade.StopLossLimit;
    [Obsolete("Use Trade.SignalProcessor.")]
    public SignalProcessorModel SignalProcessor => Trade.SignalProcessor;
    [Obsolete("Use Fund.FundBalance.")]
    public FundBalanceModel FundBalance => Fund.FundBalance;
    [Obsolete("Use EventSourcing.EventStreamId.")]
    public EventStreamIdModel EventStreamId => EventSourcing.EventStreamId;
    [Obsolete("Use EventSourcing.EventNameId.")]
    public EventNameIdModel EventNameId => EventSourcing.EventNameId;
    [Obsolete("Use MarketDataFeed.FuturesOpenPrice.")]
    public FuturesOpenPriceModel FuturesOpenPrice => MarketDataFeed.FuturesOpenPrice;
    [Obsolete("Use MarketDataFeed.VixFuturesOpenPrice.")]
    public VixFuturesOpenPriceModel VixFuturesOpenPrice =>
        MarketDataFeed.VixFuturesOpenPrice;
    [Obsolete("Use MarketDataFeed.StreamingRequestId.")]
    public StreamingRequestIdModel StreamingRequestId => MarketDataFeed.StreamingRequestId;
    [Obsolete("Use Application.SequenceCounter.")]
    public SequenceCounterModel SequenceCounter => Application.SequenceCounter;
    [Obsolete("Use MarketData.RiskFreeRate.")]
    public RiskFreeRateModel RiskFreeRate => MarketData.RiskFreeRate;
    [Obsolete("Use MarketDataAnalytics.FuturesRsiSignal.")]
    public FuturesRsiSignalModel FuturesRsiSignal => MarketDataAnalytics.FuturesRsiSignal;
    [Obsolete("Use MarketDataAnalytics.FuturesRsiDailySignal.")]
    public FuturesRsiDailySignalModel FuturesRsiDailySignal =>
        MarketDataAnalytics.FuturesRsiDailySignal;
    [Obsolete("Use EventSourcing.EventProjectorState.")]
    public EventProjectorStateModel EventProjectorState =>
        EventSourcing.EventProjectorState;
}
