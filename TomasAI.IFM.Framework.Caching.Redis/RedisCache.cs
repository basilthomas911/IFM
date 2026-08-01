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
/// <param name="timeProvider">UTC clock used to select TTL or absolute expiration.</param>
public class RedisCache(
    IConnectionMultiplexer redisMultiplexor,
    TimeProvider? timeProvider = null) : IRedisCache
{
    readonly IConnectionMultiplexer _redisMultiplexor = IsArgumentNull.Set(redisMultiplexor);
    readonly IDatabase _redis = IsArgumentNull.Set(redisMultiplexor.GetDatabase());
    readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

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
    /// Removes keys whose names start with the supplied literal prefix from the current Redis database.
    /// Uses incremental server-side key scanning and never flushes the database.
    /// </summary>
    /// <param name="prefix">Literal key prefix. Redis pattern characters are escaped.</param>
    /// <returns>The number of keys deleted.</returns>
    public long RemoveByPrefix(string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        var pattern = new RedisValue(EscapePattern(prefix) + "*");
        var seen = new HashSet<RedisKey>();
        foreach (var endpoint in _redisMultiplexor.GetEndPoints(configuredOnly: false))
        {
            var server = _redisMultiplexor.GetServer(endpoint);
            if (!server.IsConnected
                || server.IsReplica
                || server.ServerType == ServerType.Sentinel)
            {
                continue;
            }
            foreach (var key in server.Keys(
                database: _redis.Database,
                pattern: pattern,
                pageSize: 250))
            {
                seen.Add(key);
            }
        }
        long deleted = 0;
        foreach (var key in seen)
        {
            if (_redis.KeyDelete(key))
            {
                ++deleted;
            }
        }
        return deleted;
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
    /// Sets a value with a renewable TTL bounded by a hard absolute expiration.
    /// Redis receives whichever deadline occurs first.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="value">Cache value.</param>
    /// <param name="absoluteExpiry">Hard UTC expiration that renewals cannot extend.</param>
    /// <param name="ttl">TTL applied when it expires before the hard deadline.</param>
    public void Set(
        string key,
        string value,
        DateTimeOffset absoluteExpiry,
        TimeSpan ttl)
    {
        var redisKey = new RedisKey(key);
        var redisValue = new RedisValue(value);
        _redis.StringSet(
            redisKey,
            redisValue,
            GetExpiration(absoluteExpiry, ttl),
            ValueCondition.Always);
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
    /// Asynchronously sets a value with a renewable TTL bounded by a hard absolute expiration.
    /// Redis receives whichever deadline occurs first.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="value">Cache value.</param>
    /// <param name="absoluteExpiry">Hard UTC expiration that renewals cannot extend.</param>
    /// <param name="ttl">TTL applied when it expires before the hard deadline.</param>
    public async Task SetAsync(
        string key,
        string value,
        DateTimeOffset absoluteExpiry,
        TimeSpan ttl)
    {
        var redisKey = new RedisKey(key);
        var redisValue = new RedisValue(value);
        await _redis.StringSetAsync(
            redisKey,
            redisValue,
            GetExpiration(absoluteExpiry, ttl),
            ValueCondition.Always);
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

    private Expiration GetExpiration(
        DateTimeOffset absoluteExpiry,
        TimeSpan ttl)
    {
        if (ttl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ttl),
                "The cache TTL must be positive.");
        }

        var absoluteExpiryUtc = absoluteExpiry.ToUniversalTime();
        var remaining = absoluteExpiryUtc - _timeProvider.GetUtcNow();
        if (remaining <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(absoluteExpiry),
                "The absolute cache expiration must be in the future.");
        }

        return remaining <= ttl
            ? new Expiration(absoluteExpiryUtc.UtcDateTime)
            : new Expiration(ttl);
    }

    private static string EscapePattern(string prefix)
    {
        var escaped = new System.Text.StringBuilder(prefix.Length);
        foreach (var character in prefix)
        {
            if (character is '*' or '?' or '[' or ']' or '\\')
            {
                escaped.Append('\\');
            }
            escaped.Append(character);
        }
        return escaped.ToString();
    }
}
