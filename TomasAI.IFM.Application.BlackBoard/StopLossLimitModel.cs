using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Application.Blackboard;

/// <summary>
///stop loss limit blackboard model constructor
/// </summary>
/// <param name="redisCache"></param>
/// <param name="jsonSerializer"></param>
public class StopLossLimitModel(IRedisCache redisCache, IJsonSerializer jsonSerializer)
{
    readonly string CacheName = $"{DataCacheName.StopLossLimit}";
    readonly IRedisCache _redisCache = redisCache;
    readonly IJsonSerializer _jsonSerializer = jsonSerializer;

    /// <summary>
    /// return cached stop loss limit
    /// </summary>
    /// <param name="optionTradeId"></param>
    /// <returns></returns>
    public TradePlanStopLossLimitReadModel? Get(OptionTradeEntityId optionTradeId)
    {
        var key = $"{CacheName}:{optionTradeId.Format()}";
        var value = _redisCache.Get(key);
        return !string.IsNullOrEmpty(value)
            ? _jsonSerializer.Deserialize<TradePlanStopLossLimitReadModel>(value)
            : default;
    }

    /// <summary>
    /// cache stop loss limit
    /// </summary>
    /// <param name="optionTradeId"></param>
    /// <param name="stopLossLimit"></param>
    public void Set(OptionTradeEntityId optionTradeId, TradePlanStopLossLimitReadModel stopLossLimit)
    {
        var key = $"{CacheName}:{optionTradeId.Format()}";
        var value = _jsonSerializer.Serialize(stopLossLimit);
        _redisCache.Set(key, value);
    }

    /// <summary>
    /// check if stop loss limit exists in cache
    /// </summary>
    /// <param name="optionTradeId"></param>
    /// <returns></returns>
    public bool Exists(OptionTradeEntityId optionTradeId)
    {
        var key = _jsonSerializer.Serialize(new { CacheName, optionTradeId });
        return _redisCache.TryGet(key, out _);
    }

    /// <summary>
    /// remove stop loss limit from cache
    /// </summary>
    /// <param name="optionTradeId"></param>
    public void Remove(OptionTradeEntityId optionTradeId)
    {
        var key = _jsonSerializer.Serialize(new { CacheName, optionTradeId });
        _redisCache.Remove(key);
    }
}
