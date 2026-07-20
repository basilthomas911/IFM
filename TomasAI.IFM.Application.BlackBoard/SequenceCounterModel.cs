using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Shared.Caching;

namespace TomasAI.IFM.Application.Blackboard;

/// <summary>
/// sequence counter blackboard model constructor
/// </summary>
/// <param name="redisCache"></param>
public class SequenceCounterModel(IRedisCache redisCache)
{
    readonly string CacheName = $"{DataCacheName.SequenceCounter}";
    readonly IRedisCache _redisCache = redisCache;

    /// <summary>
    /// return cached sequence counter value
    /// </summary>
    /// <typeparam name="T">The enum type used to identify the counter.</typeparam>
    /// <param name="counterName"></param>
    /// <returns>the current counter value, or 0 if not set</returns>
    public long Get<T>(T counterName) where T : struct, Enum
    {
        var key = $"{CacheName}:{counterName}";
        var value = _redisCache.Get(key);
        return !string.IsNullOrEmpty(value)
            ? long.Parse(value)
            : 0;
    }

    /// <summary>
    /// atomically increment sequence counter and return the new value
    /// </summary>
    /// <typeparam name="T">The enum type used to identify the counter.</typeparam>
    /// <param name="counterName"></param>
    /// <returns>the incremented counter value</returns>
    public long Increment<T>(T counterName) where T : struct, Enum
    {
        var key = $"{CacheName}:{counterName}";
        return _redisCache.Increment(key);
    }
}
