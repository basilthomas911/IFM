using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Application.Blackboard;

/// <summary>
/// hedge position trade id blackboard model constructor
/// </summary>
/// <param name="redisCache"></param>
/// <param name="jsonSerializer"></param>
public class HedgePositionTradeIdModel(IRedisCache redisCache, IJsonSerializer jsonSerializer)
{
    readonly string CacheName = $"{DataCacheName.HedgePositionTradeId}";
    readonly IRedisCache _redisCache = redisCache;
    readonly IJsonSerializer _jsonSerializer = jsonSerializer;

    // <summary>
    /// return cached hedge position trade id
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public OptionTradeEntityId? Get(TradePositionEntityId id)
    {
        var key = $"{CacheName}:{id.Format()}";
        var value = _redisCache.Get(key);
        return !string.IsNullOrEmpty(value)
            ? _jsonSerializer.Deserialize<OptionTradeEntityId>(value)
            : default;
    }

    /// <summary>
    /// cache hedge position trade id
    /// </summary>
    /// <param name="id"></param>
    /// <param name="optionTradeId"></param>
    public void Set(TradePositionEntityId id, OptionTradeEntityId optionTradeId)
    {
        var key = $"{CacheName}:{id.Format()}";
        var value = _jsonSerializer.Serialize(optionTradeId);
        _redisCache.Set(key, value);
    }
}
