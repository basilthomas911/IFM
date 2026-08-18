using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.Blackboard;

public interface IEventSourcingBlackboard
{
    DomainEventsCacheModel DomainEvents { get; }
    EventStreamIdCacheModel EventStreamId { get; }
    EventNameIdCacheModel EventNameId { get; }
    EventProjectorStateCacheModel EventProjectorState { get; }
}

public interface IFundBlackboard
{
    FundBalanceCacheModel FundBalance { get; }
}

public interface IMarketDataBlackboard
{
    RiskFreeRateCacheModel RiskFreeRate { get; }
}

public interface IMarketDataAnalyticsBlackboard
{
    FuturesItiSignalAveragePredictedTrendDeltaCacheModel
        FuturesItiSignalAveragePredictedTrendDelta { get; }
    FuturesItiSignalAveragePredictedTrendDeltaRangeCacheModel
        FuturesItiSignalAveragePredictedTrendDeltaRange { get; }
    FuturesItiSignalMDICacheModel FuturesItiSignalMDI { get; }
    FuturesRsiSignalCacheModel FuturesRsiSignal { get; }
    FuturesRsiDailySignalCacheModel FuturesRsiDailySignal { get; }
}

public interface IMarketDataFeedBlackboard
{
    FuturesTickDataStreamingParameterCacheModel FuturesTickDataStreamingParameter { get; }
    FuturesOptionTickDataStreamingParameterCacheModel
        FuturesOptionTickDataStreamingParameter { get; }
    FuturesEodDataCacheModel FuturesEodData { get; }
    VixFuturesEodDataCacheModel VixFuturesEodData { get; }
    FuturesEodDataRangeCacheModel FuturesEodDataRange { get; }
    NormalCurveTableCacheModel NormalCurveTable { get; }
    VixFuturesContractIdCacheModel VixFuturesContractId { get; }
    VixFuturesOpenPriceCacheModel VixFuturesOpenPrice { get; }
    StreamingRequestIdCacheModel StreamingRequestId { get; }
}

public interface IMarketDataSecuritiesBlackboard
{
    IDatabentoContractMappingCache DatabentoContractMapping { get; }
    FuturesContractCacheModel FuturesContract { get; }
    FuturesContractSymbolCacheModel FuturesContractSymbol { get; }
}

public interface IReferenceBlackboard
{
    ReferenceLookupCacheModel ReferenceLookup { get; }
}

public interface ITradeBlackboard
{
    OptionTradeCacheModel OptionTrade { get; }
    TradePositionActionCacheModel TradePositionAction { get; }
    TradePlanForwardLossLimitCacheModel TradePlanForwardLossLimit { get; }
    HedgePositionTradeIdCacheModel HedgePositionTradeId { get; }
    TradeOrderCacheModel TradeOrder { get; }
    IronCondorMDILimitCacheModel IronCondorMDILimit { get; }
    ForwardLossRatioMapCacheModel ForwardLossRatioMap { get; }
    StopLossLimitCacheModel StopLossLimit { get; }
    SignalProcessorCacheModel SignalProcessor { get; }
}

internal sealed class EventSourcingBlackboard(
    IRedisCache redisCache,
    IJsonSerializer jsonSerializer) : IEventSourcingBlackboard
{
    public DomainEventsCacheModel DomainEvents { get; } = new(redisCache, jsonSerializer);
    public EventStreamIdCacheModel EventStreamId { get; } = new(redisCache, jsonSerializer);
    public EventNameIdCacheModel EventNameId { get; } = new(redisCache, jsonSerializer);
    public EventProjectorStateCacheModel EventProjectorState { get; } =
        new(redisCache, jsonSerializer);
}

internal sealed class FundBlackboard(
    IRedisCache redisCache,
    IJsonSerializer jsonSerializer) : IFundBlackboard
{
    public FundBalanceCacheModel FundBalance { get; } = new(redisCache, jsonSerializer);
}

internal sealed class MarketDataBlackboard(
    IRedisCache redisCache,
    IJsonSerializer jsonSerializer) : IMarketDataBlackboard
{
    public RiskFreeRateCacheModel RiskFreeRate { get; } = new(redisCache, jsonSerializer);
}

internal sealed class MarketDataAnalyticsBlackboard(
    IRedisCache redisCache,
    IJsonSerializer jsonSerializer) : IMarketDataAnalyticsBlackboard
{
    public FuturesItiSignalAveragePredictedTrendDeltaCacheModel
        FuturesItiSignalAveragePredictedTrendDelta { get; } =
            new(redisCache, jsonSerializer);

    public FuturesItiSignalAveragePredictedTrendDeltaRangeCacheModel
        FuturesItiSignalAveragePredictedTrendDeltaRange { get; } =
            new(redisCache, jsonSerializer);

    public FuturesItiSignalMDICacheModel FuturesItiSignalMDI { get; } =
        new(redisCache, jsonSerializer);

    public FuturesRsiSignalCacheModel FuturesRsiSignal { get; } =
        new(redisCache, jsonSerializer);

    public FuturesRsiDailySignalCacheModel FuturesRsiDailySignal { get; } =
        new(redisCache, jsonSerializer);
}

internal sealed class MarketDataFeedBlackboard : IMarketDataFeedBlackboard
{
    internal MarketDataFeedBlackboard(
        IRedisCache redisCache,
        IJsonSerializer jsonSerializer)
    {
        FuturesTickDataStreamingParameter = new(redisCache);
        FuturesOptionTickDataStreamingParameter = new(redisCache, jsonSerializer);
        FuturesEodData = new(redisCache, jsonSerializer);
        VixFuturesEodData = new(redisCache, jsonSerializer);
        FuturesEodDataRange = new(redisCache, jsonSerializer);
        NormalCurveTable = new(redisCache, jsonSerializer);
        VixFuturesContractId = new(redisCache, jsonSerializer);
        VixFuturesOpenPrice = new(redisCache, jsonSerializer);
        StreamingRequestId = new(redisCache, jsonSerializer);
    }

    public FuturesTickDataStreamingParameterCacheModel FuturesTickDataStreamingParameter { get; }
    public FuturesOptionTickDataStreamingParameterCacheModel
        FuturesOptionTickDataStreamingParameter { get; }
    public FuturesEodDataCacheModel FuturesEodData { get; }
    public VixFuturesEodDataCacheModel VixFuturesEodData { get; }
    public FuturesEodDataRangeCacheModel FuturesEodDataRange { get; }
    public NormalCurveTableCacheModel NormalCurveTable { get; }
    public VixFuturesContractIdCacheModel VixFuturesContractId { get; }
    public VixFuturesOpenPriceCacheModel VixFuturesOpenPrice { get; }
    public StreamingRequestIdCacheModel StreamingRequestId { get; }
}

internal sealed class MarketDataSecuritiesBlackboard(
    IRedisCache redisCache,
    IJsonSerializer jsonSerializer) : IMarketDataSecuritiesBlackboard
{
    public IDatabentoContractMappingCache DatabentoContractMapping { get; } =
        new DatabentoContractMappingCache(redisCache, jsonSerializer);

    public FuturesContractCacheModel FuturesContract { get; } =
        new(redisCache, jsonSerializer);

    public FuturesContractSymbolCacheModel FuturesContractSymbol { get; } =
        new(redisCache, jsonSerializer);
}

internal sealed class ReferenceBlackboard(
    IRedisCache redisCache,
    IJsonSerializer jsonSerializer) : IReferenceBlackboard
{
    public ReferenceLookupCacheModel ReferenceLookup { get; } =
        new(redisCache, jsonSerializer);
}

internal sealed class TradeBlackboard(
    IRedisCache redisCache,
    IJsonSerializer jsonSerializer) : ITradeBlackboard
{
    public OptionTradeCacheModel OptionTrade { get; } = new(redisCache, jsonSerializer);
    public TradePositionActionCacheModel TradePositionAction { get; } =
        new(redisCache, jsonSerializer);
    public TradePlanForwardLossLimitCacheModel TradePlanForwardLossLimit { get; } =
        new(redisCache, jsonSerializer);
    public HedgePositionTradeIdCacheModel HedgePositionTradeId { get; } =
        new(redisCache, jsonSerializer);
    public TradeOrderCacheModel TradeOrder { get; } = new(redisCache, jsonSerializer);
    public IronCondorMDILimitCacheModel IronCondorMDILimit { get; } =
        new(redisCache, jsonSerializer);
    public ForwardLossRatioMapCacheModel ForwardLossRatioMap { get; } =
        new(redisCache, jsonSerializer);
    public StopLossLimitCacheModel StopLossLimit { get; } = new(redisCache, jsonSerializer);
    public SignalProcessorCacheModel SignalProcessor { get; } =
        new(redisCache, jsonSerializer);
}
