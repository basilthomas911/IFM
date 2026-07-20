using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.Caching;

namespace TomasAI.IFM.Application.Blackboard;

/// <summary>
/// Provides methods to cache and retrieve reference lookup data using a Redis cache.
/// </summary>
/// <param name="redisCache">The Redis cache instance used to store and retrieve reference lookup data.</param>
/// <param name="jsonSerializer">The JSON serializer used to serialize and deserialize reference lookup data.</param>
public class ReferenceLookupModel(IRedisCache redisCache, IJsonSerializer jsonSerializer)
{
    readonly string CacheName = $"{DataCacheName.ReferenceLookup}";
    readonly IRedisCache _redisCache = redisCache;
    readonly IJsonSerializer _jsonSerializer = jsonSerializer;

    /// <summary>
    /// return cached reference data
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public Dictionary<string, List<string>>? Get()
    {
        var key = $"{CacheName}";
        var value = _redisCache.Get(key);
        return !string.IsNullOrEmpty(value)
            ? _jsonSerializer.Deserialize<Dictionary<string, List<string>>>(value)
            : default;
    }

    /// <summary>
    /// cache reference lookup data
    /// </summary>
    /// <param name="referenceLookup"></param>
    public void Set(Dictionary<string, List<string>> referenceLookup)
    {
        var key = $"{CacheName}";
        var value = _jsonSerializer.Serialize(referenceLookup);
        _redisCache.Set(key, value);
    }
}
