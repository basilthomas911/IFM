using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Blackboard;

/// <summary>
/// futures contract blackboard model constructor
/// </summary>
/// <param name="redisCache"></param>
/// <param name="jsonSerializer"></param>
public class FuturesContractCacheModel(IRedisCache redisCache, IJsonSerializer jsonSerializer)
{
    readonly string CacheName = $"{DataCacheName.FuturesContract}";
    readonly IRedisCache _redisCache = redisCache;
    readonly IJsonSerializer _jsonSerializer = jsonSerializer;

    /// <summary>
    /// return cached futures contract
    /// </summary>
    /// <param name="futuresContractId"></param>
    /// <returns></returns>
    public FuturesContractV3ReadModel? Get(FuturesContractId futuresContractId)
    {
        var key = $"{CacheName}:{futuresContractId}";
        var value = _redisCache.Get(key);
        return !string.IsNullOrEmpty(value)
            ? _jsonSerializer.Deserialize<FuturesContractV3ReadModel>(value)
            : default;
    }

    /// <summary>
    /// cache futures contract
    /// </summary>
    /// <param name="futuresContractId"></param>
    /// <param name="futuresContract"></param>
    public void Set(FuturesContractId futuresContractId, FuturesContractV3ReadModel futuresContract)
    {
        var key = $"{CacheName}:{futuresContractId}";
        var value = _jsonSerializer.Serialize(futuresContract);
        _redisCache.Set(key, value);
    }
}
