using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Application.Blackboard;

/// <summary>
/// futures eod data range blackboard model constructor
/// </summary>
/// <param name="redisCache"></param>
/// <param name="jsonSerializer"></param>
public class FuturesEodDataRangeModel(IRedisCache redisCache, IJsonSerializer jsonSerializer)
{
    readonly string CacheName = $"{DataCacheName.FuturesEodDataRange}";
    readonly IRedisCache _redisCache = redisCache;
    readonly IJsonSerializer _jsonSerializer = jsonSerializer;

    /// <summary>
    /// return cached futures eod data by date range
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async ValueTask<FuturesEodDataV2ReadModel[]> GetAsync(string contractId, DateOnly valueDate, Func<string, DateOnly, DateOnly, ValueTask<FuturesEodDataV2ReadModel[]>> getFuturesEodDataAsync)
    {
        var cacheId = FuturesEodDataId.Create(contractId, valueDate);
        var key = $"{CacheName}:{cacheId.Format()}";
        var value = _redisCache.Get(key);
        if (string.IsNullOrEmpty(value))
        {
            var startDate = valueDate.AddYears(-1);
            var futuresEodData = await getFuturesEodDataAsync.Invoke(contractId, startDate, valueDate);
            if (futuresEodData is not null)
            {
                value = _jsonSerializer.Serialize(futuresEodData);
                _redisCache.Set(key, value);
            }
        }
        else
        {
            var futuresEodData = _jsonSerializer.Deserialize<FuturesEodDataV2ReadModel[]>(value);
            if (futuresEodData is not null && (futuresEodData.Length == 0 || futuresEodData.First().ValueDate != valueDate))
            {
                var startDate = valueDate.AddYears(-1);
                futuresEodData = await getFuturesEodDataAsync.Invoke(contractId, startDate, valueDate);
                if (futuresEodData is not null)
                {
                    value = _jsonSerializer.Serialize(futuresEodData);
                    _redisCache.Set(key, value);
                }
            }
        }
        value = _redisCache.Get(key);
        return !string.IsNullOrEmpty(value)
            ? _jsonSerializer.Deserialize<FuturesEodDataV2ReadModel[]>(value) ?? []
            : [];
    }

    /// <summary>
    /// remove cached futures eod data range
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    public void Remove(string contractId, DateOnly valueDate)
    {
        var cacheId = FuturesEodDataId.Create(contractId, valueDate);
        var key = $"{CacheName}:{cacheId.Format()}";
        _redisCache.Remove(key);
    }
}
