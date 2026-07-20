using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Shared.MarketDataFeed;

namespace TomasAI.IFM.Application.Blackboard;

/// <summary>
/// Provides caching operations for streaming request identifiers, allowing retrieval, storage, and removal
/// of request ID mappings in the Redis cache.
/// </summary>
/// <param name="redisCache">The Redis cache instance used to persist streaming request identifiers.</param>
/// <param name="jsonSerializer">The JSON serializer used to serialize and deserialize request identifier values.</param>
public class StreamingRequestIdModel(IRedisCache redisCache, IJsonSerializer jsonSerializer)
{
    readonly string CacheName = $"{DataCacheName.StreamingRequestId}";
    readonly IRedisCache _redisCache = redisCache;
    readonly IJsonSerializer _jsonSerializer = jsonSerializer;

    /// <summary>
    /// Returns the cached streaming request identifier associated with the specified key.
    /// </summary>
    /// <param name="requestIdKey">The unique key identifying the streaming request. Cannot be null or empty.</param>
    /// <returns>The cached streaming request identifier, or -1 if no entry is found.</returns>
    public StreamingRequestId Get(string requestIdKey)
    {
        var key = $"{CacheName}:{requestIdKey}";
        var value = _redisCache.Get(key);
        return !string.IsNullOrEmpty(value)
            ? _jsonSerializer.Deserialize<StreamingRequestId>(value)
            : new();
    }

    /// <summary>
    /// Retrieves a cached streaming request identifier associated with the specified request ID.
    /// </summary>
    /// <remarks>This method attempts to locate a serialized streaming request identifier in the cache using
    /// the provided request ID. If the cache contains a value for the specified ID, it is deserialized and returned. If
    /// no value is found, the method returns <see langword="null"/>.</remarks>
    /// <param name="requestId">The unique identifier of the streaming request to retrieve from the cache.</param>
    /// <returns>A <see cref="StreamingRequestId"/> object if the request ID exists in the cache; otherwise, <see
    /// langword="null"/>.</returns>
    public StreamingRequestId Get(int requestId)
    {
        var key = $"{CacheName}:{requestId}";
        var value = _redisCache.Get(key);
        return !string.IsNullOrEmpty(value)
            ? _jsonSerializer.Deserialize<StreamingRequestId>(value)
            : new();
    }

    /// <summary>
    /// Stores the specified streaming request identifier in the cache using both its contract and request identifiers
    /// as keys.
    /// </summary>
    /// <remarks>The cached entries expire after one day. Both the contract and request identifiers are used
    /// as separate cache keys, each storing the serialized representation of the streaming request
    /// identifier.</remarks>
    /// <param name="requestId">The streaming request identifier to store in the cache. This parameter must not be null and should contain valid
    /// contract and request identifiers.</param>
    public void Set(StreamingRequestId requestId)
    {
        var key = $"{CacheName}:{requestId.OptionContract.ContractId}";
        var value = _jsonSerializer.Serialize(requestId);
        var expiry = TimeSpan.FromDays(1); 
        _redisCache.Set(key, value, expiry);
        key = $"{CacheName}:{requestId.RequestId}";
        value = _jsonSerializer.Serialize(requestId);
        _redisCache.Set(key, value, expiry);
    }

    /// <summary>
    /// Removes the cached streaming request identifier associated with the specified key from the Redis cache.
    /// </summary>
    /// <param name="requestIdKey">The unique key identifying the streaming request to remove. Cannot be null or empty.</param>
    public void Remove(StreamingRequestId requestId)
    {
        var key = $"{CacheName}:{requestId.OptionContract.ContractId}";
        _redisCache.Remove(key);
        key = $"{CacheName}:{requestId.RequestId}";
        _redisCache.Remove(key);
    }
}
