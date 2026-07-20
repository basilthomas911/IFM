using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Application.Blackboard;

/// <summary>
/// normal curve table blackboard model constructor
/// </summary>
/// <param name="redisCache"></param>
/// <param name="jsonSerializer"></param>
public class NormalCurveTableModel(IRedisCache redisCache, IJsonSerializer jsonSerializer)
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
        NormalCurveTableReadModel? normalCurveTable;
        var key = $"{CacheName}:{valueDate:yyyyMMdd}";
        var value = _redisCache.Get(key);
        if (string.IsNullOrEmpty(value))
        {
            normalCurveTable = await getNormalCurveTableAsync!.Invoke();
            value = _jsonSerializer.Serialize(normalCurveTable);
            _redisCache.Set(key, value);
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
