using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Shared.MarketDataFeed;

namespace TomasAI.IFM.Application.Blackboard;

/// <summary>
/// futures option tick data streaming parameter blackboard model constructor
/// </summary>
/// <param name="redisCache"></param>
/// <param name="jsonSerializer"></param>
public class FuturesOptionTickDataStreamingParameterModel(IRedisCache redisCache, IJsonSerializer jsonSerializer)
{
    readonly string CacheName = $"{DataCacheName.FuturesOptionTickDataStreamingParameter}";
    readonly IRedisCache _redisCache = redisCache;
    readonly IJsonSerializer _jsonSerializer = jsonSerializer;

    /// <summary>
    /// get futures option tick data streaming parameter
    /// </summary>
    /// <param name="requestId"></param>
    /// <returns></returns>
    public FuturesOptionTickDataStreamingParameter? Get(int requestId)
    {
        var key = $"{CacheName}:{requestId}";
        var value = _redisCache.Get(key);
        return !string.IsNullOrEmpty(value)
            ? _jsonSerializer.Deserialize<FuturesOptionTickDataStreamingParameter>(value)
            : default;
    }

    /// <summary>
    /// cache futures option tick data streaming parameter
    /// </summary>
    /// <param name="requestId"></param>
    /// <param name="futuresOptionTickDataStreamingParameter"></param>
    public void Set(int requestId, FuturesOptionTickDataStreamingParameter futuresOptionTickDataStreamingParameter)
    {
        var key = $"{CacheName}:{requestId}";
        var value = _jsonSerializer.Serialize(futuresOptionTickDataStreamingParameter);
        _redisCache.Set(key, value);
    }
}
