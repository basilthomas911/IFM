using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Application.Blackboard;

/// <summary>
/// futures tick data blackboard model constructor
/// </summary>
/// <param name="redisCache"></param>
/// <param name="jsonSerializer"></param>
public class FuturesTickDataModel(IRedisCache redisCache, IJsonSerializer jsonSerializer)
{
    readonly string CacheName = $"{DataCacheName.FuturesTickData}";
    readonly IRedisCache _redisCache = redisCache;
    readonly IJsonSerializer _jsonSerializer = jsonSerializer;

    /// <summary>
    /// return cached futures tick data
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public FuturesTickDataV2ReadModel Get(string contractId, DateOnly valueDate)
    {
        var key = $"{CacheName}:{contractId},{valueDate:yyyyMMdd}";
        var value = _redisCache.Get(key);
        return !string.IsNullOrEmpty(value)
            ? _jsonSerializer.Deserialize<FuturesTickDataV2ReadModel>(value)
            : FuturesTickDataV2ReadModel.Default;
    }

    /// <summary>
    /// cache futures tick data
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="futuresTickData"></param>
    public void Set(string contractId, DateOnly valueDate, FuturesTickDataV2ReadModel futuresTickData)
    {
        var key = $"{CacheName}:{contractId},{valueDate:yyyyMMdd}";
        var value = _jsonSerializer.Serialize(futuresTickData);
        _redisCache.Set(key, value);
    }
}
