using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Application.Blackboard;

/// <summary>
/// forward loss ratio map model constructor
/// </summary>
/// <param name="redisCache"></param>
/// <param name="jsonSerializer"></param>
public class ForwardLossRatioMapModel(IRedisCache redisCache, IJsonSerializer jsonSerializer)
{
    readonly string CacheName = $"{DataCacheName.ForwardLossRatioMap}";

    /// <summary>
    /// return cached  forward loss ratio map
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public Dictionary<DateOnly, ICollection<TradePlanForwardLossRatioReadModel>> Get(DateOnly valueDate)
    {
        var id = $"{valueDate:yyyyMMdd}";
        var key = $"{CacheName}:{id}";
        var value = redisCache.Get(key);
        return !string.IsNullOrEmpty(value)
            ? jsonSerializer.Deserialize<Dictionary<DateOnly, ICollection<TradePlanForwardLossRatioReadModel>>>(value) ?? []
            : [];
    }

    /// <summary>
    /// return true if cached forward loss ratio map exists
    /// </summary>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public bool Exists(DateOnly valueDate)
    {
        var id = $"{valueDate:yyyyMMdd}";
        var key = $"{CacheName}:{id}";
        return redisCache.TryGet(key, out _);
    }

    /// <summary>
    /// cache  forward loss ratio map
    /// </summary>
    /// <param name="valueDate"></param>
    /// <param name="forwardLossRatioMap"></param>
    public void Set(DateOnly valueDate, Dictionary<DateOnly, ICollection<TradePlanForwardLossRatioReadModel>> forwardLossRatioMap)
    {
        var id = $"{valueDate:yyyyMMdd}";
        var key = $"{CacheName}:{id}";
        var value = jsonSerializer.Serialize(forwardLossRatioMap);
        redisCache.Set(key, value);
    }
}
