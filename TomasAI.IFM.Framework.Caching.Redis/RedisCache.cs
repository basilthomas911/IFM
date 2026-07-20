using StackExchange.Redis;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Framework.Caching.Redis;

/// <summary>
/// Provides a wrapper around Redis cache operations, enabling the storage, retrieval, and removal of cached data.
/// </summary>
/// <remarks>This class uses an <see cref="IConnectionMultiplexer"/> to interact with a Redis database. It
/// provides synchronous and asynchronous methods for common cache operations, such as retrieving, setting, and removing
/// values.</remarks>
/// <param name="redisMultiplexor"></param>
public class RedisCache(IConnectionMultiplexer redisMultiplexor) : IRedisCache
{
    readonly IDatabase _redis = IsArgumentNull.Set(redisMultiplexor.GetDatabase());

    /// <summary>
    /// return cached value
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public string? Get(string key)
    {
        var redisKey = new RedisKey(key);
        return _redis.StringGet(redisKey);
    }

    /// <summary>
    /// return cached value if it exists in cache
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public bool TryGet(string key, out string? value)
    {
        var redisKey = new RedisKey(key);
        value = _redis.KeyExists(redisKey)
            ? _redis.StringGet(redisKey)
            : default;
        return !string.IsNullOrEmpty(value);
    }

    /// <summary>
    /// return cached value
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public async Task<string?> GetAsync(string key)
    {
        var redisKey = new RedisKey(key);
        return await _redis.StringGetAsync(redisKey);
    }

    /// <summary>
    /// remove cached value
    /// </summary>
    /// <param name="key"></param>
    public void Remove(string key)
    {
        _ = _redis.Execute("DEL", key);
    }

    /// <summary>
    /// remove cached value
    /// </summary>
    /// <param name="key"></param>
    public async Task RemoveAsync(string key)
    {
        var redisKey = new RedisKey(key);
        var redisValue = await _redis.StringGetAsync(redisKey);
        if (!redisValue.IsNullOrEmpty)
        {
            redisValue = new RedisValue(string.Empty);
            await _redis.StringSetAsync(redisKey, redisValue);
        }
    }

    /// <summary>
    /// set cached value
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    public void Set(string key, string value)
    {
        var redisKey = new RedisKey(key);
        var redisValue = new RedisValue(value);
        _redis.StringSet(redisKey, redisValue);
    }

    /// <summary>
    /// set cached value with expiry
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <param name="expiry"></param>
    public void Set(string key, string value, TimeSpan expiry)
    {
        var redisKey = new RedisKey(key);
        var redisValue = new RedisValue(value);
        _redis.StringSet(redisKey, redisValue, expiry);
    }

    /// <summary>
    /// set cached value
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    public async Task SetAsync(string key, string value)
    {
        var redisKey = new RedisKey(key);
        var redisValue = new RedisValue(value);
        await _redis.StringSetAsync(redisKey, redisValue);
    }

    /// <summary>
    /// set cached value with expiry
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <param name="expiry"></param>
    public async Task SetAsync(string key, string value, TimeSpan expiry)
    {
        var redisKey = new RedisKey(key);
        var redisValue = new RedisValue(value);
        await _redis.StringSetAsync(redisKey, redisValue, expiry);
    }

    /// <summary>
    /// Deletes all keys from the current Redis database.
    /// </summary>
    /// <remarks>This operation removes all keys from the currently selected Redis database.  Use with
    /// caution, as this action is irreversible and will result in the loss of all data in the database.</remarks>
    public void DeleteAllKeys()
    {
        // FLUSHDB removes all keys from the current database
        _redis.Execute("FLUSHDB");
    }

    /// <summary>
    /// Atomically increments the integer value stored at the specified key by one and returns the new value.
    /// If the key does not exist, it is initialized to zero before performing the increment.
    /// </summary>
    /// <param name="key">The cache key whose value should be incremented.</param>
    /// <returns>The value of the key after the increment.</returns>
    public long Increment(string key)
    {
        var redisKey = new RedisKey(key);
        return _redis.StringIncrement(redisKey);
    }
}
