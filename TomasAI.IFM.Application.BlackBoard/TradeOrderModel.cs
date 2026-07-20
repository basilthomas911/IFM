using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Shared.TradeOrder;
using TomasAI.IFM.Shared.TradeOrder.ViewModels;

namespace TomasAI.IFM.Application.Blackboard;

/// <summary>
/// trade order blackboard model constructor
/// </summary>
/// <param name="redisCache"></param>
/// <param name="jsonSerializer"></param>
public class TradeOrderModel(IRedisCache redisCache, IJsonSerializer jsonSerializer)
{
    readonly string CacheName = $"{DataCacheName.TradeOrder}";
    readonly IRedisCache _redisCache = redisCache;
    readonly IJsonSerializer _jsonSerializer = jsonSerializer;

    /// <summary>
    /// return cached trade order
    /// </summary>
    /// <param name="tradeOrderId"></param>
    /// <returns></returns>
    public TradeOrderReadModel? Get(TradeOrderEntityId tradeOrderId)
    {
        var key = $"{CacheName}:{tradeOrderId.Format()}";
        var value = _redisCache.Get(key);
        return !string.IsNullOrEmpty(value)
            ? _jsonSerializer.Deserialize<TradeOrderReadModel>(value)
            : default;
    }

    /// <summary>
    /// cache trade ticket
    /// </summary>
    /// <param name="tradeOrderId"></param>
    /// <param name="tradeOrder"></param>
    public void Set(TradeOrderEntityId tradeOrderId, TradeOrderReadModel tradeOrder)
    {
        var key = $"{CacheName}:{tradeOrderId.Format()}";
        var value = _jsonSerializer.Serialize(tradeOrder);
        _redisCache.Set(key, value);
    }
}
