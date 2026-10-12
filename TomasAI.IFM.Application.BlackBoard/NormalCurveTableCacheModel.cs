using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Blackboard;

/// <summary>
/// normal curve table blackboard model constructor
/// </summary>
/// <param name="redisCache"></param>
/// <param name="jsonSerializer"></param>
public class NormalCurveTableCacheModel(IRedisCache redisCache, IJsonSerializer jsonSerializer)
{
    readonly string CacheName = $"{DataCacheName.NormalCurveTable}";
    readonly IRedisCache _redisCache = redisCache;
    readonly IJsonSerializer _jsonSerializer = jsonSerializer;

    /// <summary>
    /// get cached normal curve table
    /// </summary>
    /// <param name="valueDate"></param>
    /// <param name="getNormalCurveTableAsync"></param>
    /// <returns></returns>
    public async ValueTask<NormalCurveTableReadModel?> GetAsync(DateOnly valueDate, Func<ValueTask<NormalCurveTableReadModel>> getNormalCurveTableAsync)
    {
        ArgumentNullException.ThrowIfNull(getNormalCurveTableAsync);
        NormalCurveTableReadModel? normalCurveTable;
        var key = $"{CacheName}:{valueDate:yyyyMMdd}";
        var value = await _redisCache.GetAsync(key).ConfigureAwait(false);
        if (string.IsNullOrEmpty(value))
        {
            normalCurveTable = await (getNormalCurveTableAsync!.Invoke()).ConfigureAwait(false);
            value = _jsonSerializer.Serialize(normalCurveTable);
            await _redisCache.SetAsync(key, value).ConfigureAwait(false);
        }
        normalCurveTable = _jsonSerializer.Deserialize<NormalCurveTableReadModel>(value);
        return normalCurveTable;
    }

    /// <summary>
    /// remove cached normal curve table
    /// </summary>
    /// <param name="valueDate"></param>
    public void Remove(DateOnly valueDate)
    {
        var key = $"{CacheName}:{valueDate:yyyyMMdd}";
        _redisCache.Remove(key);
    }
}
