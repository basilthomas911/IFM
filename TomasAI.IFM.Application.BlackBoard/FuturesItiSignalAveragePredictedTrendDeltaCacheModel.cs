using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.Blackboard;

public class FuturesItiSignalAveragePredictedTrendDeltaCacheModel
{
    readonly string CacheName = $"{DataCacheName.FuturesItiSignalAveragePredictedTrendDelta}";
    readonly IRedisCache? _redisCache;
    readonly IJsonSerializer _jsonSerializer;

    /// <summary>
    /// futures iti signal average predicted trend delta blackboard model constructor
    /// </summary>
    /// <param name="redisCache"></param>
    /// <param name="jsonSerializer"></param>
    public FuturesItiSignalAveragePredictedTrendDeltaCacheModel(IRedisCache redisCache, IJsonSerializer jsonSerializer)
    {
        _redisCache = redisCache;
        _jsonSerializer = jsonSerializer;
    }

    /// <summary>
    /// for BDD tests useage only
    /// </summary>
    public FuturesItiSignalAveragePredictedTrendDeltaCacheModel() : this(default!, default!)
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
        ArgumentNullException.ThrowIfNull(getFuturesItiSignalAveragePredictedTrendDelta);
        var key = $"{CacheName}:{contractId}. {valueDate:yyyyMMdd}";
        var value = await _redisCache!.GetAsync(key).ConfigureAwait(false);
        if (string.IsNullOrEmpty(value))
        {
            var avgPredictedTrendDelta = await (getFuturesItiSignalAveragePredictedTrendDelta(contractId, valueDate)).ConfigureAwait(false);
            if (avgPredictedTrendDelta is null)
                return default;
            value = _jsonSerializer.Serialize(avgPredictedTrendDelta);
            await _redisCache.SetAsync(key, value).ConfigureAwait(false);
        }
        return _jsonSerializer.Deserialize<FuturesItiSignalAveragePredictedTrendDeltaDataModel>(value);
    }
}
