using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text;
using ServiceStack;
using ServiceStack.Text;
using ServiceStack.Redis;
using ServiceStack.DataAnnotations;
using Microsoft.Extensions.Logging;

using TomasAI.IFM.Shared.Caching;

namespace TomasAI.IFM.Framework.Caching.Redis
{
    /// <summary>
    /// data cache service that uses redis as backing store
    /// </summary>
    public class RedisDataCacheService : IDataCacheService
    {
        private readonly IRedisClient _redisClient;
        private readonly ILogger _logger;

        public const string ERR_Add_CacheItem = "RedisDataCacheService.Add: unable to add cache item to redis cache";
        public const string ERR_Add_CacheNameUndefined = "RedisDataCacheService.Add: cacheName is undefined";
        public const string ERR_Add_CacheKeyExists = "RedisDataCacheService.Add: {0} cacheKey exists already";
        public const string ERR_Add_CacheKeyEmpty = "RedisDataCacheService.Add: cacheKey is empty";
        public const string ERR_Add_CacheItemEmpty = "RedisDataCacheService.Add: cacheItem is empty";
        public const string ERR_Exists_CacheNameUndefined = "RedisDataCacheService.Exists: cacheName is undefined";
        public const string ERR_Exists_CacheKeyEmpty = "RedisDataCacheService.Exists: cacheKey is empty";
        public const string ERR_Get_CacheNameUndefined = "RedisDataCacheService.Get cacheName is undefined";
        public const string ERR_Get_CacheDoesNotExist = "RedisDataCacheService.Get: {0} cache does not exist";
        public const string ERR_Get_CacheKeyDoesNotExist = "RedisDataCacheService.Get: {0} cache key does not exist";
        public const string ERR_Get_CacheKeyEmpty = "RedisDataCacheService.Get: cacheKey is empty";
        public const string ERR_Remove_CacheNameUndefined = "RedisDataCacheService.Remove cacheName is undefined";
        public const string ERR_Remove_CacheKeyEmpty = "RedisDataCacheService.Remove: cacheKey is empty";
        public const string ERR_Remove_CacheDoesNotExist = "RedisDataCacheService.Remove: {0} cache does not exist";
        public const string ERR_Remove_CacheKeyDoesNotExist = "RedisDataCacheService.Remove: {0} cache key does not exist";

        private const string Cache_Name = "CacheName";
        private const string Cache_Key = "CacheKey";
        private const string Cache_Item = "CacheItem";

        /// <summary>
        /// redis data cache service constructor
        /// </summary>
        /// <param name="redisClientManager"></param>
        public RedisDataCacheService(IRedisClientsManager redisClientManager, ILogger logger)
        {
            _redisClient = redisClientManager.GetClient();
            _logger = logger;
        }

        public int Count => throw new NotImplementedException();

        /// <summary>
        /// add cache item to redis data cache
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="cacheName"></param>
        /// <param name="cacheKey"></param>
        /// <param name="cacheItem"></param>
        public void Add<TKey, TValue>(DataCacheName cacheName, TKey cacheKey, TValue cacheItem) where TValue : class
        {
            try
            {
                if (cacheName == DataCacheName.Undefined)
                    throw new DataCacheServiceException(ERR_Add_CacheNameUndefined);
                if (((object)cacheKey) is null)
                    throw new DataCacheServiceException(ERR_Add_CacheKeyEmpty);
                if (cacheItem is null)
                    throw new DataCacheServiceException(ERR_Add_CacheItemEmpty);
                var redis = _redisClient.As<TValue>();
                var dataCache = redis.GetHash<TKey>($"{cacheName}");
                if (redis.HashContainsEntry(dataCache, cacheKey))
                    throw new DataCacheServiceException(string.Format(ERR_Add_CacheKeyExists, $"{cacheKey}"));
                redis.SetEntryInHash(dataCache, cacheKey, cacheItem);
            }
            catch(DataCacheServiceException ex)
            {
                _logger.LogError(ex, ERR_Add_CacheItem);
                throw ex;
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, ERR_Add_CacheItem);
                throw new DataCacheServiceException(ERR_Add_CacheItem, ex);
            }
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public bool Exists<TKey>(DataCacheName cacheName, TKey cacheKey)
        {
            throw new NotImplementedException();
        }

        public TValue Get<TKey, TValue>(DataCacheName cacheName, TKey cacheKey) where TValue : class
        {
            throw new NotImplementedException();
        }

        public void Remove<TKey>(DataCacheName cacheName, TKey cacheKey)
        {
            throw new NotImplementedException();
        }

        public void Remove<TKey>(DataCacheName cacheName)
        {
            throw new NotImplementedException();
        }

        public void Update<TKey, TValue>(DataCacheName cacheName, TKey cacheKey, TValue cacheItem) where TValue : class
        {
            throw new NotImplementedException();
        }
    }
}
