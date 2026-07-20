using Newtonsoft.Json;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Framework.Caching;

namespace TomasAI.IFM.Application.Blackboard;

/// <summary>
/// futures tick data streaming parameter blackboard model constructor
/// </summary>
/// <param name="redisCache"></param>
public class FuturesTickDataStreamingParameterModel(IRedisCache redisCache)
{
    readonly string CacheName = $"{DataCacheName.FuturesTickDataStreamingParameter}";
    readonly IRedisCache _redisCache = redisCache;

    /// <summary>
    /// return cachedfutures tick data streaming parameter
    /// </summary>
    /// <param name="requestId"></param>
    /// <returns></returns>
    public FuturesTickDataStreamingParameter Get(int requestId)
    {
        var key = $"{CacheName}:{requestId}";
        var value = _redisCache.Get(key);
        return !string.IsNullOrEmpty(value)
            ? JsonConvert.DeserializeObject<FuturesTickDataStreamingParameter>(value)
            : new FuturesTickDataStreamingParameter();
    }

    /// <summary>
    /// set futures tick data streaming parameter
    /// </summary>
    /// <param name="requestId"></param>
    /// <param name="futuresTickDataStreamingParameter"></param>
    public void Set(int requestId, FuturesTickDataStreamingParameter futuresTickDataStreamingParameter)
    {
        var key = $"{CacheName}:{requestId}";
        var value = JsonConvert.SerializeObject(futuresTickDataStreamingParameter, Formatting.None);
        _redisCache.Set(key, value);
    }
}
