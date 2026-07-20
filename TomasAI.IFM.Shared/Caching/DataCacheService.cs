using System.Collections.Concurrent;

namespace TomasAI.IFM.Shared.Caching;

public class DataCacheService : IDataCacheService
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

    static object? _root;
    readonly ConcurrentDictionary<DataCacheName, Dictionary<string, object>> _dataCacheMap;

    /// <summary>
    /// DataCacheService constructor
    /// </summary>
    public DataCacheService()
    {
        _root ??= new object();
        _dataCacheMap = new ();
    }

    /// <summary>
    /// return count of named caches 
    /// </summary>
    public int Count => _dataCacheMap.Count;

    /// <summary>
    /// add cache item to named cache
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="cacheName"></param>
    /// <param name="cacheKey"></param>
    /// <param name="cacheItem"></param>
    public void Add<TKey, TValue>(DataCacheName cacheName, TKey cacheKey, TValue cacheItem) where TValue : class
    {
        if (cacheName == DataCacheName.Undefined)
            throw new ArgumentNullException(nameof(cacheName), ERR_Add_CacheNameUndefined);
        if ((cacheKey) is null)
            throw new ArgumentNullException($"{cacheName}", ERR_Add_CacheKeyEmpty);
        if (cacheItem is null)
            throw new ArgumentNullException($"{cacheName}", ERR_Add_CacheItemEmpty);
        lock (_root!)
        {
            if (!_dataCacheMap.ContainsKey(cacheName))
                _dataCacheMap.TryAdd(cacheName,[ ]);
            var dataCache = _dataCacheMap[cacheName];
            if (dataCache.ContainsKey($"{cacheKey}"))
                throw new ArgumentNullException($"{cacheName}", string.Format(ERR_Add_CacheKeyExists, $"{cacheKey}"));
            dataCache.Add($"{cacheKey}", cacheItem);
        }
    }

    /// <summary>
    /// update cache item in named cache
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="cacheName"></param>
    /// <param name="cacheKey"></param>
    /// <param name="cacheItem"></param>
    public void Update<TKey, TValue>(DataCacheName cacheName, TKey cacheKey, TValue cacheItem) where TValue : class
    {
        if (cacheName == DataCacheName.Undefined)
            throw new ArgumentNullException(nameof(cacheName), ERR_Add_CacheNameUndefined);
        if (cacheKey is null)
            throw new ArgumentNullException($"{cacheName}", ERR_Add_CacheKeyEmpty);
        if (cacheItem is null)
            throw new ArgumentNullException($"{cacheName}", ERR_Add_CacheItemEmpty);
        lock (_root!)
        {
            if (!_dataCacheMap.ContainsKey(cacheName))
                _dataCacheMap.TryAdd(cacheName, []);
            var dataCache = _dataCacheMap[cacheName];
            if (dataCache.ContainsKey($"{cacheKey}"))
                dataCache.Remove($"{cacheKey}");
            dataCache.Add($"{cacheKey}", cacheItem);
        }
    }

    /// <summary>
    /// true if cache item exists in named cache
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <param name="cacheName"></param>
    /// <param name="cacheKey"></param>
    /// <returns></returns>
    public bool Exists<TKey>(DataCacheName cacheName, TKey cacheKey)
    {
        if (cacheName == DataCacheName.Undefined)
            throw new ArgumentNullException(nameof(cacheName), ERR_Exists_CacheNameUndefined);
        if (!_dataCacheMap.TryGetValue(cacheName, out Dictionary<string, object>? dataCache))
            return false;
        if (cacheKey is null)
            throw new ArgumentNullException($"{cacheName}", ERR_Exists_CacheKeyEmpty);
        lock (_root!)
        {
            return dataCache.ContainsKey($"{cacheKey}");
        }
    }

    /// <summary>
    /// get cache item from named cache
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="cacheName"></param>
    /// <param name="cacheKey"></param>
    /// <returns></returns>
    public TValue Get<TKey, TValue>(DataCacheName cacheName, TKey cacheKey) where TValue : class
    {
        if (cacheName == DataCacheName.Undefined)
            throw new ArgumentNullException(nameof(cacheName), ERR_Get_CacheNameUndefined);
        if (!_dataCacheMap.TryGetValue(cacheName, out Dictionary<string, object>? dataCache))
        {
            dataCache = [];
            _dataCacheMap.TryAdd(cacheName, dataCache);
        }
        if (cacheKey is null)
            throw new ArgumentNullException($"{cacheName}", ERR_Get_CacheKeyEmpty);
        lock (_root!)
        {
            if (!dataCache.ContainsKey($"{cacheKey}"))
                dataCache.Add($"{cacheKey}", null!);
            var cacheItem = dataCache[$"{cacheKey}"] as TValue;
            return cacheItem!;
        }
    }

    /// <summary>
    /// remove cache item from named cache
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <param name="cacheName"></param>
    /// <param name="cacheKey"></param>
    public void Remove<TKey>(DataCacheName cacheName, TKey cacheKey)
    {
        if (cacheName == DataCacheName.Undefined)
            throw new ArgumentNullException(nameof(cacheName), ERR_Remove_CacheNameUndefined);
        if (!_dataCacheMap.TryGetValue(cacheName, out Dictionary<string, object>? dataCache))
            throw new InvalidOperationException(string.Format(ERR_Remove_CacheDoesNotExist, cacheName));
        if ((cacheKey) is null)
            throw new ArgumentNullException(nameof(cacheName), ERR_Remove_CacheKeyEmpty);
        lock (_root!)
        {
            if (!dataCache.ContainsKey($"{cacheKey}"))
                throw new InvalidOperationException(string.Format(ERR_Remove_CacheKeyDoesNotExist, $"{cacheKey}"));
            if (dataCache.ContainsKey($"{cacheKey}"))
                dataCache.Remove($"{cacheKey}");
        }
    }

    /// <summary>
    /// remove named cache 
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <param name="cacheName"></param>
    /// <param name="cacheKey"></param>
    public void Remove<TKey>(DataCacheName cacheName)
    {
        if (cacheName == DataCacheName.Undefined)
            throw new ArgumentNullException(nameof(cacheName), ERR_Remove_CacheNameUndefined);
        if (!_dataCacheMap.ContainsKey(cacheName))
            throw new InvalidOperationException(string.Format(ERR_Remove_CacheDoesNotExist, cacheName));
        lock (_root!)
        {
            _dataCacheMap.TryRemove(cacheName, out Dictionary<string, object>? value);
        }
    }

    /// <summary>
    /// clear all caches
    /// </summary>
    public void Clear()
    {
        lock (_root!)
        {
            _dataCacheMap.Clear();
        }
    }

}
