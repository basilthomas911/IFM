using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Application.Blackboard;

/// <summary>
/// futures contract blackboard model constructor
/// </summary>
/// <param name="redisCache"></param>
/// <param name="jsonSerializer"></param>
public class FuturesContractModel(IRedisCache redisCache, IJsonSerializer jsonSerializer)
{
    readonly string CacheName = $"{DataCacheName.FuturesContract}";
    readonly IRedisCache _redisCache = redisCache;
    readonly IJsonSerializer _jsonSerializer = jsonSerializer;

    /// <summary>
    /// return cached futures contract
    /// </summary>
    /// <param name="futuresContractId"></param>
    /// <returns></returns>
    public FuturesContractV2ReadModel? Get(FuturesContractId futuresContractId)
    {
        var key = $"{CacheName}:{futuresContractId}";
        var value = _redisCache.Get(key);
        return !string.IsNullOrEmpty(value)
            ? _jsonSerializer.Deserialize<FuturesContractV2ReadModel>(value)
            : default;
    }

    /// <summary>
    /// cache futures contract
    /// </summary>
    /// <param name="futuresContractId"></param>
    /// <param name="futuresContract"></param>
    public void Set(FuturesContractId futuresContractId, FuturesContractV2ReadModel futuresContract)
    {
        var key = $"{CacheName}:{futuresContractId}";
        var value = _jsonSerializer.Serialize(futuresContract);
        _redisCache.Set(key, value);
    }
}
