using System.Linq.Expressions;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Shared.Storage;

public class DbCache : IDbCache
{
    private Dictionary<string, IDbSingleCache> _cache;

    /// <summary>
    /// create db cache object
    /// </summary>
    public DbCache()
    {
        _cache = new Dictionary<string, IDbSingleCache>
        {
            { "EntityTypeIdMap", new DbSingleCache<string, long>() },
            { "EventTypeIdMap", new DbSingleCache<string, long>() },
            { "FuturesEodDataMap", new DbSingleCache<string, FuturesEodDataV2ReadModel>() },
            { "FuturesOptionTickDataMap", new DbSingleCache<string, FuturesOptionTickDataV2ReadModel>() }
        };
    }

    /// <summary>
    /// return cache value
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="cacheNameExpr"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    public TValue Get<TKey, TValue>(Expression<Func<IDbCacheName, string>> cacheNameExpr, TKey key, Func<TValue> getValue = null)
    {
        var cache = _cache[GetCacheName(cacheNameExpr)];
        if (!(cache is DbSingleCache<TKey, TValue>))
            throw new InvalidOperationException("DbCache.Get: invalid cache expression");
        return ((dynamic)cache).Get(key, getValue);
    }

    /// <summary>
    /// clear caches
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="cacheNameExpr"></param>
    public void Clear(Expression<Func<IDbCacheName, string>> cacheNameExpr)
        => ((dynamic)_cache[GetCacheName(cacheNameExpr)]).Clear();

    /// <summary>
    /// return cache count
    /// </summary>
    /// <param name="cacheNameExpr"></param>
    /// <returns></returns>
    public int Count(Expression<Func<IDbCacheName, string>> cacheNameExpr)
        => ((dynamic)_cache[GetCacheName(cacheNameExpr)]).Count();

    /// <summary>
    /// load cache with existing entries
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="cacheNameExpr"></param>
    /// <param name="cacheEntries"></param>
    public void Load<TKey, TValue>(Expression<Func<IDbCacheName, string>> cacheNameExpr, IDictionary<TKey, TValue> cacheEntries)
    {
        if (cacheEntries == null)
            throw new ArgumentException("DbCache.Load: empty cache entries");
        var cache = _cache[GetCacheName(cacheNameExpr)];
        if (! (cache is DbSingleCache<TKey,TValue>))
            throw new InvalidOperationException("DbCache.Load: invalid cache entries");
        ((dynamic)cache).Load(cacheEntries);
    }

    /// <summary>
    /// return cache name from property expression
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="cacheNameExpr"></param>
    /// <returns></returns>
    static string GetCacheName(Expression<Func<IDbCacheName, string>> cacheNameExpr)
    {
        if (cacheNameExpr == null)
            throw new ArgumentException("DbCache.GetCacheName: cache name property expression is empty");
        if (cacheNameExpr.Body is not MemberExpression propExpr)
            throw new InvalidOperationException("DbCache.GetCacheName: cache name property expression does not map to a known property");
        return propExpr.Member.Name;
    }

    private class DbSingleCache<TKey, TValue> : IDbSingleCache
    {
        Dictionary<TKey, TValue> _cache = [];

        public void Clear() => _cache.Clear();

        public int Count() => _cache.Count;

        public TValue Get(TKey key, Func<TValue> getValue = null!)
        {
            try
            {
                if (getValue != null)
                {
                    lock (_cache)
                    {
                        if (!_cache.ContainsKey((dynamic)key!))
                            _cache.Add((dynamic)key!, (dynamic)getValue()!);
                        return _cache[(dynamic)key!];
                    }
                }
                else
                    return _cache.ContainsKey((dynamic)key!) ? _cache[(dynamic)key!] : default(TValue)!;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("DbCache.Get: cache key is invalid", ex);
            }
        }

        public void Load(IDictionary<TKey, TValue> cacheEntries)
        {
            _cache = new Dictionary<TKey, TValue>((dynamic)(cacheEntries as IDictionary<TKey, TValue>)!);
        }
    }

}
