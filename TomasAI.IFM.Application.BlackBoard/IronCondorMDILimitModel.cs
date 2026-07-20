using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Application.Blackboard;

/// <summary>
/// iron condor MDI limit blackboard model constructor
/// </summary>
/// <param name="redisCache"></param>
/// <param name="jsonSerializer"></param>
public class IronCondorMDILimitModel(IRedisCache redisCache, IJsonSerializer jsonSerializer)
{
    readonly string CacheName = $"{DataCacheName.IronCondorMDILimit}";
    readonly IRedisCache _redisCache = redisCache;
    readonly IJsonSerializer _jsonSerializer = jsonSerializer;

    /// <summary>
    /// return cached iron condor MDI limit
    /// </summary>
    /// <param name="id"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public IronCondorMDILimitDataModel? Get(OptionTradeEntityId id, DateOnly valueDate)
    {
        var key = $"{CacheName}:{id.Format()},{valueDate:yyyyMMdd}";
        var value = _redisCache.Get(key);
        return !string.IsNullOrEmpty(value)
            ? _jsonSerializer.Deserialize<IronCondorMDILimitDataModel>(value)
            : default;
    }

    /// <summary>
    /// cache iron condor MDI limit
    /// </summary>
    /// <param name="id"></param>
    /// <param name="valueDate"></param>
    /// <param name="ironCondorMDILimit"></param>
    public void Set(OptionTradeEntityId id, DateOnly valueDate, IronCondorMDILimitDataModel ironCondorMDILimit)
    {
        var key = $"{CacheName}:{id.Format()},{valueDate:yyyyMMdd}";
        var value = _jsonSerializer.Serialize(ironCondorMDILimit);
        _redisCache.Set(key, value);
    }
}
