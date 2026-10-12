using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.Blackboard;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S101", Justification = "Preserve the published domain and protocol type name.")]
public class FuturesItiSignalMDICacheModel
{
    readonly string CacheName = $"{DataCacheName.FuturesItiSignalMDI}";
    readonly IRedisCache? _redisCache;
    readonly IJsonSerializer _jsonSerializer;

    /// <summary>
    /// futures iti MDI model constructor
    /// </summary>
    /// <param name="redisCache"></param>
    /// <param name="jsonSerializer"></param>
    public FuturesItiSignalMDICacheModel(IRedisCache redisCache, IJsonSerializer jsonSerializer)
    {
        _redisCache = redisCache;
        _jsonSerializer = jsonSerializer;
    }

    /// <summary>
    /// for BDD tests useage only
    /// </summary>
    public FuturesItiSignalMDICacheModel() : this(default!, default!)
    {
    }

    /// <summary>
    /// return cached futures iti MDI 
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <param name="getFuturesItiSignalMDI"></param>
    /// <returns></returns>
    public virtual async ValueTask<FuturesItiSignalMDIV2ReadModel[]> GetAsync(string contractId, DateOnly valueDate,
        Func<string, DateOnly, Task<FuturesItiSignalMDIV2ReadModel[]>> getFuturesItiSignalMDI)
    {
        ArgumentNullException.ThrowIfNull(getFuturesItiSignalMDI);
        var key = $"{CacheName}:{contractId}. {valueDate:yyyyMMdd}";
        var value = await _redisCache.GetAsync(key).ConfigureAwait(false);
        if (string.IsNullOrEmpty(value))
        {
            var futuresItiMDIDistribution = await (getFuturesItiSignalMDI(contractId, valueDate)).ConfigureAwait(false);
            if (futuresItiMDIDistribution is null)
                return [];
            value = _jsonSerializer.Serialize(futuresItiMDIDistribution);
            TomasAI.IFM.Shared.EventModelActor.RealtimeActorGeneration.ThrowIfRetired();
        await _redisCache.SetAsync(key, value).ConfigureAwait(false);
        }
        return _jsonSerializer.Deserialize<FuturesItiSignalMDIV2ReadModel[]>(value) ?? [];
    }

    /// <summary>
    ///  cache futures iti MDI distribution
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <param name="futuresItiMDIDistribution"></param>
    /// <returns></returns>
    public void Set(string contractId, DateOnly valueDate, FuturesItiSignalMDIV2ReadModel[] futuresItiSignalMDIViewModel)
    {
        TomasAI.IFM.Shared.EventModelActor.RealtimeActorGeneration.ThrowIfRetired();
        var key = $"{CacheName}:{contractId}. {valueDate:yyyyMMdd}";
        var value = _redisCache.Get(key);
        if (!string.IsNullOrEmpty(value))
        {
            value = _jsonSerializer.Serialize(futuresItiSignalMDIViewModel);
            TomasAI.IFM.Shared.EventModelActor.RealtimeActorGeneration.ThrowIfRetired();
        _redisCache.Set(key, value);
        }
    }
}
