using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Application.Blackboard;

/// <summary>
/// futures option tick data blackboard model constructor
/// </summary>
/// <param name="redisCache"></param>
/// <param name="jsonSerializer"></param>
public class FuturesOptionTickDataModel(IRedisCache redisCache, IJsonSerializer jsonSerializer)
{
    readonly string CacheName = $"{DataCacheName.FuturesOptionTickData}";
    readonly IRedisCache _redisCache = redisCache;
    readonly IJsonSerializer _jsonSerializer = jsonSerializer;

    /// <summary>
    /// return cached futures option tick data
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public FuturesOptionTickDataV2ReadModel Get(string contractId, DateOnly valueDate)
    {
        var key = $"{CacheName}:{contractId}.{valueDate:yyyyMMdd}";
        var value = _redisCache.Get(key);
        return !string.IsNullOrEmpty(value)
            ? _jsonSerializer.Deserialize<FuturesOptionTickDataV2ReadModel>(value)
            : FuturesOptionTickDataV2ReadModel.Default;
    }

    /// <summary>
    /// cache futures option tick data
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="futuresOptionTickData"></param>
    public void Set(string contractId, DateOnly valueDate, FuturesOptionTickDataV2ReadModel futuresOptionTickData)
    {
        var key = $"{CacheName}:{contractId}.{valueDate:yyyyMMdd}";
        var value = _jsonSerializer.Serialize(futuresOptionTickData);
        _redisCache.Set(key, value);
    }
}
