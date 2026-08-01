using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.Blackboard;

public interface IApplicationBlackboard
{
    SequenceCounterModel SequenceCounter { get; }
}

public interface IEventSourcingBlackboard
{
    DomainEventsModel DomainEvents { get; }
    EventStreamIdModel EventStreamId { get; }
    EventNameIdModel EventNameId { get; }
    EventProjectorStateModel EventProjectorState { get; }
}

public interface IFundBlackboard
{
    FundBalanceModel FundBalance { get; }
}

public interface IMarketDataBlackboard
{
    RiskFreeRateModel RiskFreeRate { get; }
}

public interface IMarketDataAnalyticsBlackboard
{
    FuturesItiSignalAveragePredictedTrendDeltaModel
        FuturesItiSignalAveragePredictedTrendDelta { get; }
    FuturesItiSignalAveragePredictedTrendDeltaRangeModel
        FuturesItiSignalAveragePredictedTrendDeltaRange { get; }
    FuturesItiSignalMDIModel FuturesItiSignalMDI { get; }
    FuturesRsiSignalModel FuturesRsiSignal { get; }
    FuturesRsiDailySignalModel FuturesRsiDailySignal { get; }
}

public interface IMarketDataFeedBlackboard
{
    FuturesTickDataModel FuturesTickData { get; }
    FuturesOptionTickDataModel FuturesOptionTickData { get; }
    FuturesOptionTickDataModel FuturesOptionTickPriceData { get; }
    FuturesTickDataStreamingParameterModel FuturesTickDataStreamingParameter { get; }
    FuturesOptionTickDataStreamingParameterModel
        FuturesOptionTickDataStreamingParameter { get; }
    FuturesEodDataModel FuturesEodData { get; }
    VixFuturesEodDataModel VixFuturesEodData { get; }
    FuturesEodDataRangeModel FuturesEodDataRange { get; }
    NormalCurveTableModel NormalCurveTable { get; }
    VixFuturesContractIdModel VixFuturesContractId { get; }
    FuturesOptionQuoteModel FuturesOptionQuote { get; }
    FuturesOptionQuoteDataModel FuturesOptionQuoteData { get; }
    FuturesOpenPriceModel FuturesOpenPrice { get; }
    VixFuturesOpenPriceModel VixFuturesOpenPrice { get; }
    StreamingRequestIdModel StreamingRequestId { get; }
}

public interface IMarketDataSecuritiesBlackboard
{
    IDatabentoContractMappingCache DatabentoContractMapping { get; }
    FuturesContractModel FuturesContract { get; }
    FuturesContractSymbolModel FuturesContractSymbol { get; }
}

public interface IReferenceBlackboard
{
    ReferenceLookupModel ReferenceLookup { get; }
}

public interface ITradeBlackboard
{
    OptionTradeModel OptionTrade { get; }
    TradePositionActionModel TradePositionAction { get; }
    TradePlanForwardLossLimitModel TradePlanForwardLossLimit { get; }
    HedgePositionTradeIdModel HedgePositionTradeId { get; }
    TradeOrderModel TradeOrder { get; }
    IronCondorMDILimitModel IronCondorMDILimit { get; }
    ForwardLossRatioMapModel ForwardLossRatioMap { get; }
    StopLossLimitModel StopLossLimit { get; }
    SignalProcessorModel SignalProcessor { get; }
}

internal sealed class ApplicationBlackboard(IRedisCache redisCache)
    : IApplicationBlackboard
{
    public SequenceCounterModel SequenceCounter { get; } = new(redisCache);
}

internal sealed class EventSourcingBlackboard(
    IRedisCache redisCache,
    IJsonSerializer jsonSerializer) : IEventSourcingBlackboard
{
    public DomainEventsModel DomainEvents { get; } = new(redisCache, jsonSerializer);
    public EventStreamIdModel EventStreamId { get; } = new(redisCache, jsonSerializer);
    public EventNameIdModel EventNameId { get; } = new(redisCache, jsonSerializer);
    public EventProjectorStateModel EventProjectorState { get; } =
        new(redisCache, jsonSerializer);
}

internal sealed class FundBlackboard(
    IRedisCache redisCache,
    IJsonSerializer jsonSerializer) : IFundBlackboard
{
    public FundBalanceModel FundBalance { get; } = new(redisCache, jsonSerializer);
}

internal sealed class MarketDataBlackboard(
    IRedisCache redisCache,
    IJsonSerializer jsonSerializer) : IMarketDataBlackboard
{
    public RiskFreeRateModel RiskFreeRate { get; } = new(redisCache, jsonSerializer);
}

internal sealed class MarketDataAnalyticsBlackboard(
    IRedisCache redisCache,
    IJsonSerializer jsonSerializer) : IMarketDataAnalyticsBlackboard
{
    public FuturesItiSignalAveragePredictedTrendDeltaModel
        FuturesItiSignalAveragePredictedTrendDelta { get; } =
            new(redisCache, jsonSerializer);

    public FuturesItiSignalAveragePredictedTrendDeltaRangeModel
        FuturesItiSignalAveragePredictedTrendDeltaRange { get; } =
            new(redisCache, jsonSerializer);

    public FuturesItiSignalMDIModel FuturesItiSignalMDI { get; } =
        new(redisCache, jsonSerializer);

    public FuturesRsiSignalModel FuturesRsiSignal { get; } =
        new(redisCache, jsonSerializer);

    public FuturesRsiDailySignalModel FuturesRsiDailySignal { get; } =
        new(redisCache, jsonSerializer);
}

internal sealed class MarketDataFeedBlackboard : IMarketDataFeedBlackboard
{
    internal MarketDataFeedBlackboard(
        IRedisCache redisCache,
        IJsonSerializer jsonSerializer)
    {
        FuturesTickData = new(redisCache, jsonSerializer);
        FuturesOptionTickData = new(redisCache, jsonSerializer);
        FuturesOptionTickPriceData = FuturesOptionTickData;
        FuturesTickDataStreamingParameter = new(redisCache);
        FuturesOptionTickDataStreamingParameter = new(redisCache, jsonSerializer);
        FuturesEodData = new(redisCache, jsonSerializer);
        VixFuturesEodData = new(redisCache, jsonSerializer);
        FuturesEodDataRange = new(redisCache, jsonSerializer);
        NormalCurveTable = new(redisCache, jsonSerializer);
        VixFuturesContractId = new(redisCache, jsonSerializer);
        FuturesOptionQuote = new(redisCache, jsonSerializer);
        FuturesOptionQuoteData = new(redisCache, jsonSerializer);
        FuturesOpenPrice = new(redisCache, jsonSerializer);
        VixFuturesOpenPrice = new(redisCache, jsonSerializer);
        StreamingRequestId = new(redisCache, jsonSerializer);
    }

    public FuturesTickDataModel FuturesTickData { get; }
    public FuturesOptionTickDataModel FuturesOptionTickData { get; }
    public FuturesOptionTickDataModel FuturesOptionTickPriceData { get; }
    public FuturesTickDataStreamingParameterModel FuturesTickDataStreamingParameter { get; }
    public FuturesOptionTickDataStreamingParameterModel
        FuturesOptionTickDataStreamingParameter { get; }
    public FuturesEodDataModel FuturesEodData { get; }
    public VixFuturesEodDataModel VixFuturesEodData { get; }
    public FuturesEodDataRangeModel FuturesEodDataRange { get; }
    public NormalCurveTableModel NormalCurveTable { get; }
    public VixFuturesContractIdModel VixFuturesContractId { get; }
    public FuturesOptionQuoteModel FuturesOptionQuote { get; }
    public FuturesOptionQuoteDataModel FuturesOptionQuoteData { get; }
    public FuturesOpenPriceModel FuturesOpenPrice { get; }
    public VixFuturesOpenPriceModel VixFuturesOpenPrice { get; }
    public StreamingRequestIdModel StreamingRequestId { get; }
}

internal sealed class MarketDataSecuritiesBlackboard(
    IRedisCache redisCache,
    IJsonSerializer jsonSerializer) : IMarketDataSecuritiesBlackboard
{
    public IDatabentoContractMappingCache DatabentoContractMapping { get; } =
        new DatabentoContractMappingCache(redisCache, jsonSerializer);

    public FuturesContractModel FuturesContract { get; } =
        new(redisCache, jsonSerializer);

    public FuturesContractSymbolModel FuturesContractSymbol { get; } =
        new(redisCache, jsonSerializer);
}

internal sealed class ReferenceBlackboard(
    IRedisCache redisCache,
    IJsonSerializer jsonSerializer) : IReferenceBlackboard
{
    public ReferenceLookupModel ReferenceLookup { get; } =
        new(redisCache, jsonSerializer);
}

internal sealed class TradeBlackboard(
    IRedisCache redisCache,
    IJsonSerializer jsonSerializer) : ITradeBlackboard
{
    public OptionTradeModel OptionTrade { get; } = new(redisCache, jsonSerializer);
    public TradePositionActionModel TradePositionAction { get; } =
        new(redisCache, jsonSerializer);
    public TradePlanForwardLossLimitModel TradePlanForwardLossLimit { get; } =
        new(redisCache, jsonSerializer);
    public HedgePositionTradeIdModel HedgePositionTradeId { get; } =
        new(redisCache, jsonSerializer);
    public TradeOrderModel TradeOrder { get; } = new(redisCache, jsonSerializer);
    public IronCondorMDILimitModel IronCondorMDILimit { get; } =
        new(redisCache, jsonSerializer);
    public ForwardLossRatioMapModel ForwardLossRatioMap { get; } =
        new(redisCache, jsonSerializer);
    public StopLossLimitModel StopLossLimit { get; } = new(redisCache, jsonSerializer);
    public SignalProcessorModel SignalProcessor { get; } =
        new(redisCache, jsonSerializer);
}
