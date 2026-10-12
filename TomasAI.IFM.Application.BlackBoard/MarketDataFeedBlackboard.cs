using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.Blackboard;

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
        FuturesOptionTickDataStreamingParameter
    { get; }
    public FuturesEodDataCacheModel FuturesEodData { get; }
    public VixFuturesEodDataCacheModel VixFuturesEodData { get; }
    public FuturesEodDataRangeCacheModel FuturesEodDataRange { get; }
    public NormalCurveTableCacheModel NormalCurveTable { get; }
    public VixFuturesContractIdCacheModel VixFuturesContractId { get; }
    public VixFuturesOpenPriceCacheModel VixFuturesOpenPrice { get; }
    public StreamingRequestIdCacheModel StreamingRequestId { get; }
}
