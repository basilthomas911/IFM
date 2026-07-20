using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.Blackboard;

public class FuturesItiSignalAveragePredictedTrendDeltaModel
{
    readonly string CacheName = $"{DataCacheName.FuturesItiSignalAveragePredictedTrendDelta}";
    readonly IRedisCache? _redisCache;
    readonly IJsonSerializer _jsonSerializer;

    /// <summary>
    /// futures iti signal average predicted trend delta blackboard model constructor
    /// </summary>
    /// <param name="redisCache"></param>
    /// <param name="jsonSerializer"></param>
    public FuturesItiSignalAveragePredictedTrendDeltaModel(IRedisCache redisCache, IJsonSerializer jsonSerializer)
    {
        _redisCache = redisCache;
        _jsonSerializer = jsonSerializer;
    }

    /// <summary>
    /// for BDD tests useage only
    /// </summary>
    public FuturesItiSignalAveragePredictedTrendDeltaModel() : this(default!, default!)
    {
    }

    /// <summary>
    /// return cached futures iti signal average predicted trend delta
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <param name="getFuturesItiSignalAveragePredictedTrendDelta"></param>
    /// <returns></returns>
    public async ValueTask<FuturesItiSignalAveragePredictedTrendDeltaDataModel?> GetAsync(string contractId, DateOnly valueDate, 
        Func<string, DateOnly, Task<FuturesItiSignalAveragePredictedTrendDeltaDataModel>> getFuturesItiSignalAveragePredictedTrendDelta)
    {
        var key = $"{CacheName}:{contractId}. {valueDate:yyyyMMdd}";
        var value = _redisCache!.Get(key);
        if (string.IsNullOrEmpty(value))
        {
            var avgPredictedTrendDelta = await getFuturesItiSignalAveragePredictedTrendDelta(contractId, valueDate);
            if (avgPredictedTrendDelta is null )
                return default;
            value = _jsonSerializer.Serialize(avgPredictedTrendDelta);
            _redisCache.Set(key, value);
        }
        return _jsonSerializer.Deserialize<FuturesItiSignalAveragePredictedTrendDeltaDataModel>(value);
    }
}
