using System.Collections.Concurrent;
using TomasAI.IFM.Shared.Caching;

namespace TomasAI.IFM.Framework.Caching;

/// <summary>Thread-safe named caches with atomic replacement and key-specific removal.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1707", Justification = "Preserve existing public diagnostic constant names used by callers.")]
public class LocalDataCacheService : IDataCacheService
{
    public const string ERR_Add_CacheNameUndefined = "DataCacheService.Add: cacheName is undefined";
    public const string ERR_Add_CacheKeyExists = "DataCacheService.Add: {0} cacheKey exists already";
    public const string ERR_Add_CacheKeyEmpty = "DataCacheService.Add: cacheKey is empty";
    public const string ERR_Add_CacheItemEmpty = "DataCacheService.Add: cacheItem is empty";
    public const string ERR_Exists_CacheNameUndefined = "DataCacheService.Exists: cacheName is undefined";
    public const string ERR_Exists_CacheKeyEmpty = "DataCacheService.Exists: cacheKey is empty";
    public const string ERR_Get_CacheNameUndefined = "DataCacheService.Get cacheName is undefined";
    public const string ERR_Get_CacheDoesNotExist = "DataCacheService.Get: {0} cache does not exist";
    public const string ERR_Get_CacheKeyDoesNotExist = "DataCacheService.Get: {0} cache key does not exist";
    public const string ERR_Get_CacheKeyEmpty = "DataCacheService.Get: cacheKey is empty";
    public const string ERR_Remove_CacheNameUndefined = "DataCacheService.Remove cacheName is undefined";
    public const string ERR_Remove_CacheKeyEmpty = "DataCacheService.Remove: cacheKey is empty";
    public const string ERR_Remove_CacheDoesNotExist = "DataCacheService.Remove: {0} cache does not exist";
    public const string ERR_Remove_CacheKeyDoesNotExist = "DataCacheService.Remove: {0} cache key does not exist";

    readonly ConcurrentDictionary<DataCacheName, ConcurrentDictionary<string, object>> _dataCacheMap = new();

    public int Count => _dataCacheMap.Count;

    public void Add<TKey, TValue>(DataCacheName cacheName, TKey cacheKey, TValue cacheItem) where TValue : class
    {
        ValidateCacheName(cacheName, ERR_Add_CacheNameUndefined);
        ArgumentNullException.ThrowIfNull(cacheKey);
        ArgumentNullException.ThrowIfNull(cacheItem);
        var cache = _dataCacheMap.GetOrAdd(cacheName, static _ => new(StringComparer.Ordinal));
        if (!cache.TryAdd($"{cacheKey}", cacheItem))
            throw new ArgumentException($"DataCacheService.Add: {cacheKey} cacheKey exists already", nameof(cacheKey));
    }

    public void Update<TKey, TValue>(DataCacheName cacheName, TKey cacheKey, TValue cacheItem) where TValue : class
    {
        ValidateCacheName(cacheName, ERR_Add_CacheNameUndefined);
        ArgumentNullException.ThrowIfNull(cacheKey);
        ArgumentNullException.ThrowIfNull(cacheItem);
        _dataCacheMap.GetOrAdd(cacheName, static _ => new(StringComparer.Ordinal))[$"{cacheKey}"] = cacheItem;
    }

    public bool Exists<TKey>(DataCacheName cacheName, TKey cacheKey)
    {
        ValidateCacheName(cacheName, ERR_Exists_CacheNameUndefined);
        if (!_dataCacheMap.TryGetValue(cacheName, out var cache)) return false;
        ArgumentNullException.ThrowIfNull(cacheKey);
        return cache.ContainsKey($"{cacheKey}");
    }

    public TValue Get<TKey, TValue>(DataCacheName cacheName, TKey cacheKey) where TValue : class
    {
        ValidateCacheName(cacheName, ERR_Get_CacheNameUndefined);
        if (!_dataCacheMap.TryGetValue(cacheName, out var cache))
            throw new InvalidOperationException($"DataCacheService.Get: {cacheName} cache does not exist");
        ArgumentNullException.ThrowIfNull(cacheKey);
        if (!cache.TryGetValue($"{cacheKey}", out var value))
            throw new InvalidOperationException($"DataCacheService.Get: {cacheKey} cache key does not exist");
        return (value as TValue)!;
    }

    public void Remove<TKey>(DataCacheName cacheName, TKey cacheKey)
    {
        ValidateCacheName(cacheName, ERR_Remove_CacheNameUndefined);
        if (!_dataCacheMap.TryGetValue(cacheName, out var cache))
            throw new InvalidOperationException($"DataCacheService.Remove: {cacheName} cache does not exist");
        ArgumentNullException.ThrowIfNull(cacheKey);
        if (!cache.TryRemove($"{cacheKey}", out _))
            throw new InvalidOperationException($"DataCacheService.Remove: {cacheKey} cache key does not exist");
    }

    public void Remove<TKey>(DataCacheName cacheName)
    {
        ValidateCacheName(cacheName, ERR_Remove_CacheNameUndefined);
        if (!_dataCacheMap.TryRemove(cacheName, out _))
            throw new InvalidOperationException($"DataCacheService.Remove: {cacheName} cache does not exist");
    }

    public void Clear() => _dataCacheMap.Clear();

    static void ValidateCacheName(DataCacheName cacheName, string message)
    {
        if (cacheName == DataCacheName.Undefined) throw new ArgumentException(message, nameof(cacheName));
    }
}
