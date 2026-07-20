using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Application.Blackboard;

/// <summary>
/// trade plan forward loss limit model constructor
/// </summary>
/// <param name="redisCache"></param>
/// <param name="jsonSerializer"></param>
public class TradePlanForwardLossLimitModel(IRedisCache redisCache, IJsonSerializer jsonSerializer)
{
    readonly string CacheName = $"{DataCacheName.TradePlanForwardLossLimit}";
    readonly IRedisCache _redisCache = redisCache;
    readonly IJsonSerializer _jsonSerializer = jsonSerializer;

    /// <summary>
    /// return cached trade plan forward loss limit
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public TradePlanForwardLossLimitReadModel? Get(TradePlanForwardLossLimitEntityId id)
    {
        var key = $"{CacheName}:{id.Format()}";
        var value = _redisCache.Get(key);
        return !string.IsNullOrEmpty(value)
            ? _jsonSerializer.Deserialize<TradePlanForwardLossLimitReadModel>(value)
            : default;
    }

    /// <summary>
    /// cache trade plan forward loss limit
    /// </summary>
    /// <param name="id"></param>
    /// <param name="tradePlanForwardLossLimit"></param>
    public void Set(TradePlanForwardLossLimitEntityId id, TradePlanForwardLossLimitReadModel tradePlanForwardLossLimit)
    {
        var key = $"{CacheName}:{id.Format()}";
        var value = _jsonSerializer.Serialize(tradePlanForwardLossLimit);
        _redisCache.Set(key, value);
    }

    /// <summary>
    /// remove cached trade plan forward loss limit 
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public void Remove(TradePlanForwardLossLimitEntityId id)
    {
        var key = $"{CacheName}:{id.Format()}";
        _redisCache.Remove(key);
    }
}
