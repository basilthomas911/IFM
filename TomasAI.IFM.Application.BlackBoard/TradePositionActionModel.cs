using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Application.Blackboard;

/// <summary>
/// trade position action blackboard model constructor
/// </summary>
/// <param name="redisCache"></param>
/// <param name="jsonSerializer"></param>
public class TradePositionActionModel(IRedisCache redisCache, IJsonSerializer jsonSerializer)
{
    readonly string CacheName = $"{DataCacheName.TradePositionAction}";
    readonly IRedisCache _redisCache = redisCache;
    readonly IJsonSerializer _jsonSerializer = jsonSerializer;

    /// <summary>
    /// return cached trade position action
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public TradePositionActionReadModel? Get(TradePositionEntityId id)
    {
        var key = $"{CacheName}:{id.Format()}";
        var value = _redisCache.Get(key);
        return !string.IsNullOrEmpty(value)
            ? _jsonSerializer.Deserialize<TradePositionActionReadModel>(value)
            : default;
    }

    /// <summary>
    /// cache trade position action
    /// </summary>
    /// <param name="id"></param>
    /// <param name="tradePositionAction"></param>
    public void Set(TradePositionEntityId id, TradePositionActionReadModel tradePositionAction)
    {
        var key = $"{CacheName}:{id.Format()}";
        var value = _jsonSerializer.Serialize(tradePositionAction);
        _redisCache.Set(key, value);
    }
}
