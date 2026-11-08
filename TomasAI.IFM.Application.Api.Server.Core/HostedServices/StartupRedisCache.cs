using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Caching.Redis;

namespace TomasAI.IFM.Application.Api.Server.Core.HostedServices;

/// <summary>Resolves without connecting. Async calls await the shared owner; synchronous calls require readiness and explicitly fail without blocking when Redis is unavailable.</summary>
public sealed class StartupRedisCache(RedisConnectionStartup startup, TimeProvider? clock = null) : IRedisCache
{
    RedisCache? ready;

    RedisCache RequireReady()
    {
        if (!startup.TryGetReady(out var connection))
            throw new InvalidOperationException("Redis is not ready; the asynchronous startup connection must complete before synchronous cache access.");
        return Volatile.Read(ref ready) ?? Install(new RedisCache(connection!, clock));
    }

    RedisCache Install(RedisCache cache) => Interlocked.CompareExchange(ref ready, cache, null) ?? cache;

    async Task<RedisCache> ReadyAsync()
    {
        _ = await startup.GetAsync(CancellationToken.None).ConfigureAwait(false);
        return RequireReady();
    }

    public string? Get(string key) => RequireReady().Get(key);
    public bool TryGet(string key, out string? value) => RequireReady().TryGet(key, out value);
    public void Set(string key, string value) => RequireReady().Set(key, value);
    public void Set(string key, string value, TimeSpan expiry) => RequireReady().Set(key, value, expiry);
    public void Set(string key, string value, DateTimeOffset absoluteExpiry, TimeSpan ttl) => RequireReady().Set(key, value, absoluteExpiry, ttl);
    public void Remove(string key) => RequireReady().Remove(key);
    public long RemoveByPrefix(string prefix) => RequireReady().RemoveByPrefix(prefix);
    public long Increment(string key) => RequireReady().Increment(key);
    public void DeleteAllKeys() => RequireReady().DeleteAllKeys();
    public async Task<string?> GetAsync(string key) => await (await ReadyAsync().ConfigureAwait(false)).GetAsync(key).ConfigureAwait(false);
    public async Task SetAsync(string key, string value) => await (await ReadyAsync().ConfigureAwait(false)).SetAsync(key, value).ConfigureAwait(false);
    public async Task SetAsync(string key, string value, TimeSpan expiry) => await (await ReadyAsync().ConfigureAwait(false)).SetAsync(key, value, expiry).ConfigureAwait(false);
    public async Task SetAsync(string key, string value, DateTimeOffset absoluteExpiry, TimeSpan ttl)
        => await (await ReadyAsync().ConfigureAwait(false)).SetAsync(key, value, absoluteExpiry, ttl).ConfigureAwait(false);
}
