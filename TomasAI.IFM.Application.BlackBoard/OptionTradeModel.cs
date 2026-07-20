using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.Blackboard;

/// <summary>
/// option trade blackboard model constructor
/// </summary>
/// <param name="redisCache"></param>
/// <param name="jsonSerializer"></param>
public class OptionTradeModel(IRedisCache redisCache, IJsonSerializer jsonSerializer)
{
    readonly string CacheName = $"{DataCacheName.OptionTrade}";
    readonly IRedisCache _redisCache = redisCache;
    readonly IJsonSerializer _jsonSerializer = jsonSerializer;

    /// <summary>
    /// return cached option trade
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public OptionTradeReadModel? Get(OptionTradeEntityId id)
    {
        var key = $"{CacheName}:{id.Format()}";
        var value = _redisCache.Get(key);
        return !string.IsNullOrEmpty(value)
            ? _jsonSerializer.Deserialize<OptionTradeReadModel>(value)
            : default;
    }

    /// <summary>
    /// update cached option trade
    /// </summary>
    /// <param name="id"></param>
    /// <param name="optionTrade"></param>
    public void Set(OptionTradeEntityId id, OptionTradeReadModel optionTrade)
    {
        var key = $"{CacheName}:{id.Format()}";
        var value = _jsonSerializer.Serialize(optionTrade);
        _redisCache.Set(key, value);
    }

    /// <summary>
    /// remove selected option trade from cache
    /// </summary>
    /// <param name="id"></param>
    public void Remove(OptionTradeEntityId id)
    {
        var key = $"{CacheName}:{id.Format()}";
        _redisCache.Remove(key);
    }
}
