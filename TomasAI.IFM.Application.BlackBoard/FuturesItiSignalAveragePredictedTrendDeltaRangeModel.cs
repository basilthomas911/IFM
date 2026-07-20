using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.Blackboard;

public class FuturesItiSignalAveragePredictedTrendDeltaRangeModel
{
    readonly string CacheName = $"{DataCacheName.FuturesItiSignalAveragePredictedTrendDeltaRange}";
    readonly IRedisCache _redisCache;
    readonly IJsonSerializer _jsonSerializer;

    /// <summary>
    /// futures iti signal average predicted trend delta range blackboard model constructor
    /// </summary>
    /// <param name="redisCache"></param>
    /// <param name="jsonSerializer"></param>
    public FuturesItiSignalAveragePredictedTrendDeltaRangeModel(IRedisCache redisCache, IJsonSerializer jsonSerializer)
    {
        _redisCache = redisCache;
        _jsonSerializer = jsonSerializer;
    }

    /// <summary>
    /// for BDD tests useage only
    /// </summary>
    public FuturesItiSignalAveragePredictedTrendDeltaRangeModel() : this(default!, default!)
    {
    }

    /// <summary>
    /// return cached futures iti signal average predicted trend delta
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <param name="getFuturesItiSignalAveragePredictedTrendDeltaRange"></param>
    /// <returns></returns>
    public virtual async ValueTask<FuturesItiSignalAveragePredictedTrendDeltaRangeReadModel?> GetAsync(string symbol, DateTime startDate, DateTime endDate, 
        Func<string, DateTime, DateTime, Task<FuturesItiSignalAveragePredictedTrendDeltaRangeReadModel>> getFuturesItiSignalAveragePredictedTrendDeltaRange)
    {
        var key = $"{CacheName}:{symbol}.{startDate:yyyyMMdd}.{endDate:yyyyMMdd}";
        var value = _redisCache.Get(key);
        if (string.IsNullOrEmpty(value))
        {
            var avgPredictedTrendDeltaRange = await getFuturesItiSignalAveragePredictedTrendDeltaRange(symbol, startDate, endDate);
            if (avgPredictedTrendDeltaRange is null)
                return default;
            value = _jsonSerializer.Serialize(avgPredictedTrendDeltaRange);
            _redisCache.Set(key, value);
        }
        return _jsonSerializer.Deserialize<FuturesItiSignalAveragePredictedTrendDeltaRangeReadModel>(value);
    }
}
