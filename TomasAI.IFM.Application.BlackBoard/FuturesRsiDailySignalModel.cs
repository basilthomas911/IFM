using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Application.Blackboard;

/// <summary>
/// 
/// </summary>
/// <param name="redisCache"></param>
/// <param name="jsonSerializer"></param>
public class FuturesRsiDailySignalModel(IRedisCache redisCache, IJsonSerializer jsonSerializer)
{
    readonly string CacheName = $"{DataCacheName.FuturesRsiDailySignal}";
    readonly IRedisCache _redisCache = redisCache;
    readonly IJsonSerializer _jsonSerializer = jsonSerializer;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="rsiSignalId"></param>
    /// <returns></returns>
    public FuturesRsiSignalReadModel? Get(FuturesRsiDailySignalEntityId rsiSignalId)
    {
        var key = $"{CacheName}:{rsiSignalId.Format()}";
        var value = _redisCache.Get(key);
        return !string.IsNullOrEmpty(value)
            ? _jsonSerializer.Deserialize<FuturesRsiSignalReadModel>(value)
            : default;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="rsiSignalId"></param>
    /// <param name="futuresRsiSignal"></param>
    public void Set(FuturesRsiDailySignalEntityId rsiSignalId, FuturesRsiSignalReadModel futuresRsiSignal)
    {
        var key = $"{CacheName}:{rsiSignalId.Format()}";
        var value = _jsonSerializer.Serialize(futuresRsiSignal);
        _redisCache.Set(key, value);
    }
}
