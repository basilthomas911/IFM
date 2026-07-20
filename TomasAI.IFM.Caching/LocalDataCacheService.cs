using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text;
using TomasAI.IFM.Shared.Caching;

namespace TomasAI.IFM.Framework.Caching
{
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

        private const string Cache_Name = "CacheName";
        private const string Cache_Key = "CacheKey";
        private const string Cache_Item = "CacheItem";

        private ConcurrentDictionary<DataCacheName, ConcurrentDictionary<string, object>> _dataCacheMap;

        /// <summary>
        /// DataCacheService constructor
        /// </summary>
        public LocalDataCacheService() => _dataCacheMap = new ConcurrentDictionary<DataCacheName, ConcurrentDictionary<string, object>>();

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
                throw new ArgumentNullException(Cache_Name, ERR_Add_CacheNameUndefined);
            if (((object)cacheKey) is null)
                throw new ArgumentNullException(Cache_Key, ERR_Add_CacheKeyEmpty);
            if (cacheItem is null)
                throw new ArgumentNullException(Cache_Item, ERR_Add_CacheItemEmpty);
            _dataCacheMap.TryAdd(cacheName, new ConcurrentDictionary<string, object>());
            var dataCache = _dataCacheMap[cacheName];
            if (!dataCache.TryAdd($"{cacheKey}", cacheItem))
                throw new ArgumentNullException(Cache_Key, string.Format(ERR_Add_CacheKeyExists, $"{cacheKey}"));
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
                throw new ArgumentNullException(Cache_Name, ERR_Add_CacheNameUndefined);
            if (((object)cacheKey) is null)
                throw new ArgumentNullException(Cache_Key, ERR_Add_CacheKeyEmpty);
            if (cacheItem is null)
                throw new ArgumentNullException(Cache_Item, ERR_Add_CacheItemEmpty);
            _dataCacheMap.TryAdd(cacheName, new ConcurrentDictionary<string, object>());
            var dataCache = _dataCacheMap[cacheName];
            var prevCacheItem = !dataCache.ContainsKey($"{cacheKey}") ? null : dataCache[$"{cacheKey}"] as TValue;
            if (prevCacheItem is null)
                dataCache.TryAdd($"{cacheKey}", cacheItem);
            else
                dataCache.TryUpdate($"{cacheKey}", cacheItem, prevCacheItem);
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
                throw new ArgumentNullException(Cache_Name, ERR_Exists_CacheNameUndefined);
            if (!_dataCacheMap.ContainsKey(cacheName))
                return false;
            if (((object)cacheKey) is null)
                throw new ArgumentNullException(Cache_Name, ERR_Exists_CacheKeyEmpty);
            var dataCache = _dataCacheMap[cacheName];
            return dataCache.ContainsKey($"{cacheKey}");
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
                throw new ArgumentNullException(Cache_Name, ERR_Get_CacheNameUndefined);
            if (!_dataCacheMap.ContainsKey(cacheName))
                throw new InvalidOperationException(string.Format(ERR_Get_CacheDoesNotExist, cacheName));
            if (((object)cacheKey) is null)
                throw new ArgumentNullException(Cache_Name, ERR_Get_CacheKeyEmpty);
            var dataCache = _dataCacheMap[cacheName];
            if (!dataCache.ContainsKey($"{cacheKey}"))
                throw new InvalidOperationException(string.Format(ERR_Get_CacheKeyDoesNotExist, $"{cacheKey}"));
            return dataCache[$"{cacheKey}"] as TValue;
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
                throw new ArgumentNullException(Cache_Name, ERR_Remove_CacheNameUndefined);
            if (!_dataCacheMap.ContainsKey(cacheName))
                throw new InvalidOperationException(string.Format(ERR_Remove_CacheDoesNotExist, cacheName));
            if (((object)cacheKey) is null)
                throw new ArgumentNullException(Cache_Name, ERR_Remove_CacheKeyEmpty);
            var dataCache = _dataCacheMap[cacheName];
            if (!dataCache.ContainsKey($"{cacheKey}"))
                throw new InvalidOperationException(string.Format(ERR_Remove_CacheKeyDoesNotExist, $"{cacheKey}"));
            if (dataCache.ContainsKey($"{cacheKey}"))
            {
                var value = default(ConcurrentDictionary<string, object>);
                _dataCacheMap.TryRemove(cacheName, out value);
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
                throw new ArgumentNullException(Cache_Name, ERR_Remove_CacheNameUndefined);
            if (!_dataCacheMap.ContainsKey(cacheName))
                throw new InvalidOperationException(string.Format(ERR_Remove_CacheDoesNotExist, cacheName));
            var value = default(ConcurrentDictionary<string, object>);
            _dataCacheMap.TryRemove(cacheName, out value);
        }

        /// <summary>
        /// clear all caches
        /// </summary>
        public void Clear() => _dataCacheMap.Clear();

    }
}
