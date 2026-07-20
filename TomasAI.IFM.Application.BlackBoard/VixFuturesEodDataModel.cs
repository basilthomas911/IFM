using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Application.Blackboard;

/// <summary>
/// vix futures eod data blackboard model constructor
/// </summary>
/// <param name="redisCache"></param>
/// <param name="jsonSerializer"></param>
public class VixFuturesEodDataModel(IRedisCache redisCache, IJsonSerializer jsonSerializer)
{
    readonly string CacheName = $"{DataCacheName.VixFuturesEodData}";
    readonly IRedisCache _redisCache = redisCache;
    readonly IJsonSerializer _jsonSerializer = jsonSerializer;

    /// <summary>
    /// return cached futures eod data
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public ICollection<VixFuturesEodDataReadModel> Get(string contractId, DateOnly valueDate)
    {
        var key = $"{CacheName}:{contractId}-{valueDate:yyyyMMdd}";
        var value = _redisCache.Get(key);
        return !string.IsNullOrEmpty(value)
            ? _jsonSerializer.Deserialize<ICollection<VixFuturesEodDataReadModel>>(value) ?? []
            : [];
    }

    /// <summary>
    /// cached vix futures eod data
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <param name="vixFuturesEodData"></param>
    public void Set(string contractId, DateOnly valueDate, ICollection<VixFuturesEodDataReadModel> vixFuturesEodData)
    {
        var key = $"{CacheName}:{contractId}-{valueDate:yyyyMMdd}";
        var value = _jsonSerializer.Serialize(vixFuturesEodData);
        _redisCache.Set(key, value);
    }
}
