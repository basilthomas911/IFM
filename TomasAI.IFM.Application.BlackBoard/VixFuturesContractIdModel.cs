using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.Caching;

namespace TomasAI.IFM.Application.Blackboard;

/// <summary>
/// vix futures contract id blackboard model constructor
/// </summary>
/// <param name="redisCache"></param>
/// <param name="jsonSerializer"></param>
public class VixFuturesContractIdModel(IRedisCache redisCache, IJsonSerializer jsonSerializer)
{
    readonly string CacheName = $"{DataCacheName.VixFuturesContractId}";
    readonly IRedisCache _redisCache = redisCache;
    readonly IJsonSerializer _jsonSerializer = jsonSerializer;

    /// <summary>
    /// return cached vix futures contract id
    /// </summary>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public string? Get(DateOnly valueDate)
    {
        var key = $"{CacheName}:{valueDate:yyyyMMdd}";
        var value = _redisCache.Get(key);
        return !string.IsNullOrEmpty(value)
            ? value
            : default;
    }

    /// <summary>
    /// cache vix futures contract id
    /// </summary>
    /// <param name="valueDate"></param>
    /// <param name="value"></param>
    public void Set(DateOnly valueDate, string value)
    {
        var key = $"{CacheName}:{valueDate:yyyyMMdd}";
        _redisCache.Set(key, value);
    }
}
