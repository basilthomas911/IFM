using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Application.Blackboard;

/// <summary>
///fund balance blackboard model constructor
/// </summary>
/// <param name="redisCache"></param>
/// <param name="jsonSerializer"></param>
public class FundBalanceModel(IRedisCache redisCache, IJsonSerializer jsonSerializer)
{
    readonly string CacheName = $"{DataCacheName.FundBalanceByOrderId}";
    readonly IRedisCache _redisCache = redisCache;
    readonly IJsonSerializer _jsonSerializer = jsonSerializer;

    /// <summary>
    /// return cached fund balance
    /// </summary>
    /// <param name="orderId"></param>
    /// <returns></returns>
    public FundBalanceReadModel? Get(int orderId)
    {
        var key = $"{CacheName}:{orderId}";
        var value = _redisCache.Get(key);
        return !string.IsNullOrEmpty(value)
            ? _jsonSerializer.Deserialize<FundBalanceReadModel>(value)
            : default;
    }

    /// <summary>
    /// cache fund balance
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="fundBalance"></param>
    public void Set(int orderId, FundBalanceReadModel fundBalance)
    {
        var key = $"{CacheName}:{orderId}";
        var value = _jsonSerializer.Serialize(fundBalance);
        _redisCache.Set(key, value);
    }

    /// <summary>
    /// check if fund balance exists in cache
    /// </summary>
    /// <param name="orderId"></param>
    /// <returns></returns>
    public bool Exists(int orderId)
    {
        var key = $"{CacheName}:{orderId}";
        return _redisCache.TryGet(key, out _);
    }
}
