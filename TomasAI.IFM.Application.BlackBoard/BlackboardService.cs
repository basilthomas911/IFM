using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;  
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.Blackboard;

/// <summary>
/// Provides access to various financial models and data through a caching mechanism.
/// </summary>
/// <remarks>The <see cref="BlackboardService"/> class is designed to interface with a Redis cache to provide
/// efficient access to a wide range of financial data models. It utilizes dependency injection to initialize its
/// components, ensuring that the necessary caching and serialization services are available. This service is intended
/// for use in financial applications where quick access to cached data is critical.</remarks>
public class BlackboardService : IBlackboardService
{
    public BlackboardService(IRedisCache redisCache, IJsonSerializer jsonSerializer)
    {
        IsArgumentNull.Check(redisCache);
        IsArgumentNull.Check(jsonSerializer);

        OptionTrade = new(redisCache, jsonSerializer);
        ReferenceLookup = new(redisCache, jsonSerializer);
        TradePositionAction = new(redisCache, jsonSerializer);
        TradePlanForwardLossLimit = new(redisCache, jsonSerializer);
        HedgePositionTradeId = new(redisCache, jsonSerializer);
        FuturesTickData = new(redisCache, jsonSerializer);
        FuturesOptionTickData = new(redisCache, jsonSerializer);
        FuturesOptionTickPriceData = new (redisCache, jsonSerializer);
        FuturesTickDataStreamingParameter = new(redisCache);
        FuturesOptionTickDataStreamingParameter = new(redisCache, jsonSerializer);
        FuturesEodData = new(redisCache, jsonSerializer);
        VixFuturesEodData = new(redisCache, jsonSerializer);
        FuturesEodDataRange = new(redisCache, jsonSerializer);
        NormalCurveTable = new(redisCache, jsonSerializer);
        FuturesContract = new(redisCache, jsonSerializer);
        VixFuturesContractId = new(redisCache, jsonSerializer);
        TradeOrder = new(redisCache, jsonSerializer);
        DomainEvents = new(redisCache, jsonSerializer);
        IronCondorMDILimit = new(redisCache, jsonSerializer);
        FuturesContractSymbol = new(redisCache, jsonSerializer);
        FuturesItiSignalAveragePredictedTrendDelta = new(redisCache, jsonSerializer);
        FuturesItiSignalAveragePredictedTrendDeltaRange = new(redisCache, jsonSerializer);
        FuturesItiSignalMDI = new(redisCache, jsonSerializer);
        FuturesOptionQuote = new(redisCache, jsonSerializer);
        FuturesOptionQuoteData = new(redisCache, jsonSerializer);
        ForwardLossRatioMap = new(redisCache, jsonSerializer);
        StopLossLimit = new(redisCache, jsonSerializer);
        SignalProcessor = new(redisCache, jsonSerializer);
        FundBalance = new(redisCache, jsonSerializer);
        EventStreamId = new(redisCache, jsonSerializer);
        EventNameId = new(redisCache, jsonSerializer);
        FuturesOpenPrice = new(redisCache, jsonSerializer);
        VixFuturesOpenPrice = new(redisCache, jsonSerializer);
        StreamingRequestId = new(redisCache, jsonSerializer);
        SequenceCounter = new(redisCache);
        RiskFreeRate = new(redisCache, jsonSerializer);
        FuturesRsiSignal = new(redisCache, jsonSerializer);
        FuturesRsiDailySignal = new(redisCache, jsonSerializer);
    }

    public OptionTradeModel OptionTrade { get; }
    public ReferenceLookupModel ReferenceLookup { get; }
    public TradePositionActionModel TradePositionAction { get; }
    public TradePlanForwardLossLimitModel TradePlanForwardLossLimit { get; }
    public HedgePositionTradeIdModel HedgePositionTradeId { get; }
    public FuturesTickDataModel FuturesTickData { get; }
    public FuturesOptionTickDataModel FuturesOptionTickData { get; }
    public FuturesOptionTickDataModel FuturesOptionTickPriceData { get; }
    public FuturesTickDataStreamingParameterModel FuturesTickDataStreamingParameter { get; }
    public StreamingRequestIdModel FuturesOptionTickDataStreamingParameter { get; }
    public FuturesEodDataModel FuturesEodData { get; }
    public VixFuturesEodDataModel VixFuturesEodData { get; }
    public FuturesEodDataRangeModel FuturesEodDataRange { get; }
    public NormalCurveTableModel NormalCurveTable { get; }
    public FuturesContractModel FuturesContract { get; }
    public VixFuturesContractIdModel VixFuturesContractId { get; }
    public TradeOrderModel TradeOrder { get; }
    public DomainEventsModel DomainEvents { get; }
    public IronCondorMDILimitModel IronCondorMDILimit { get; }
    public FuturesContractSymbolModel FuturesContractSymbol { get; }
    public FuturesItiSignalAveragePredictedTrendDeltaModel FuturesItiSignalAveragePredictedTrendDelta { get; }
    public FuturesItiSignalAveragePredictedTrendDeltaRangeModel FuturesItiSignalAveragePredictedTrendDeltaRange { get; }
    public FuturesItiSignalMDIModel FuturesItiSignalMDI { get; }
    public FuturesOptionQuoteModel FuturesOptionQuote { get; }
    public FuturesOptionQuoteDataModel FuturesOptionQuoteData { get; }
    public ForwardLossRatioMapModel ForwardLossRatioMap { get; }
    public StopLossLimitModel StopLossLimit { get; }
    public SignalProcessorModel SignalProcessor { get; }
    public FundBalanceModel FundBalance { get; }
    public EventStreamIdModel EventStreamId { get; }
    public EventNameIdModel EventNameId { get; }
    public FuturesOpenPriceModel FuturesOpenPrice { get; }
    public VixFuturesOpenPriceModel VixFuturesOpenPrice { get; }
    public StreamingRequestIdModel StreamingRequestId { get; }
    public SequenceCounterModel SequenceCounter { get; }
    public RiskFreeRateModel RiskFreeRate { get; }
    public FuturesRsiSignalModel FuturesRsiSignal { get; }
    public FuturesRsiDailySignalModel FuturesRsiDailySignal { get; }
}
