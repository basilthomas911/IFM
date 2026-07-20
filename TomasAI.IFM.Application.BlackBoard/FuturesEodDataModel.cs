using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Application.Blackboard;

/// <summary>
/// Represents a model for caching and retrieving futures RSI signal data in Redis.
/// </summary>
/// <param name="redisCache"></param>
/// <param name="jsonSerializer"></param>
public class FuturesEodDataModel(IRedisCache redisCache, IJsonSerializer jsonSerializer)
{
    readonly string CacheName = $"{DataCacheName.FuturesEodData}";
    readonly IRedisCache _redisCache = redisCache;
    readonly IJsonSerializer _jsonSerializer = jsonSerializer;

    /// <summary>
    /// return cached futures eod data
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public FuturesEodDataV2ReadModel? Get(string contractId, DateOnly valueDate)
    {
        var key = $"{CacheName}:{contractId}. {valueDate:yyyyMMdd}";
        var value = _redisCache.Get(key);
        return !string.IsNullOrEmpty(value)
            ? _jsonSerializer.Deserialize<FuturesEodDataV2ReadModel>(value)
            : default;
    }

    /// <summary>
    /// cache futures eod data
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <param name="futuresEodData"></param>
    public void Set(string contractId, DateOnly valueDate, FuturesEodDataV2ReadModel futuresEodData)
    {
        var key = $"{CacheName}:{contractId}. {valueDate:yyyyMMdd}";
        var value = _jsonSerializer.Serialize(futuresEodData);
        _redisCache.Set(key, value);
    }
}
