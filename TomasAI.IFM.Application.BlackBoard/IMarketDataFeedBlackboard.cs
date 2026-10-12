using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.Blackboard;

public interface IMarketDataFeedBlackboard
{
    FuturesTickDataStreamingParameterCacheModel FuturesTickDataStreamingParameter { get; }
    FuturesOptionTickDataStreamingParameterCacheModel
        FuturesOptionTickDataStreamingParameter
    { get; }
    FuturesEodDataCacheModel FuturesEodData { get; }
    VixFuturesEodDataCacheModel VixFuturesEodData { get; }
    FuturesEodDataRangeCacheModel FuturesEodDataRange { get; }
    NormalCurveTableCacheModel NormalCurveTable { get; }
    VixFuturesContractIdCacheModel VixFuturesContractId { get; }
    VixFuturesOpenPriceCacheModel VixFuturesOpenPrice { get; }
    StreamingRequestIdCacheModel StreamingRequestId { get; }
}
