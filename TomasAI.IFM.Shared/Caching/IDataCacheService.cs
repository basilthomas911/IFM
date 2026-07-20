using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Caching
{
    public interface IDataCacheService
    {
        int Count { get; }

        bool Exists<TKey>(DataCacheName cacheName, TKey cacheKey);
        TValue Get<TKey, TValue>(DataCacheName cacheName, TKey cacheKey) where TValue : class;
        void Add<TKey, TValue>(DataCacheName cacheName, TKey cacheKey, TValue cacheItem) where TValue : class;
        void Update<TKey, TValue>(DataCacheName cacheName, TKey cacheKey, TValue cacheItem) where TValue : class;
        void Remove<TKey>(DataCacheName cacheName, TKey cacheKey);
        void Remove<TKey>(DataCacheName cacheName);
        void Clear();

    }
}
