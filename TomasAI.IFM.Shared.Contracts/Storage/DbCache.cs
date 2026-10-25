using System.Linq.Expressions;

namespace TomasAI.IFM.Shared.Storage;

public class DbCache : IDbCache
{
    private readonly Dictionary<string, IDbSingleCache> _cache;

    /// <summary>
    /// create db cache object
    /// </summary>
    public DbCache()
    {
        _cache = new Dictionary<string, IDbSingleCache>(StringComparer.Ordinal)
        {
            { "EntityTypeIdMap", new DbSingleCache<string, long>() },
            { "EventTypeIdMap", new DbSingleCache<string, long>() }
        };
    }

    /// <summary>
    /// return cache value
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="cacheName"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    public TValue Get<TKey, TValue>(Expression<Func<IDbCacheName, string>> cacheName, TKey key, Func<TValue> getValue = null)
    {
        if (_cache[GetCacheName(cacheName)] is not DbSingleCache<TKey, TValue> cache)
            throw new InvalidOperationException("DbCache.Get: invalid cache expression");
        return cache.Get(key, getValue);
    }

    /// <summary>
    /// clear caches
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="cacheName"></param>
    public void Clear(Expression<Func<IDbCacheName, string>> cacheName)
        => _cache[GetCacheName(cacheName)].Clear();

    /// <summary>
    /// return cache count
    /// </summary>
    /// <param name="cacheName"></param>
    /// <returns></returns>
    public int Count(Expression<Func<IDbCacheName, string>> cacheName)
        => _cache[GetCacheName(cacheName)].Count();

    /// <summary>
    /// load cache with existing entries
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="cacheName"></param>
    /// <param name="cacheEntries"></param>
    public void Load<TKey, TValue>(Expression<Func<IDbCacheName, string>> cacheName, IDictionary<TKey, TValue> cacheEntries)
    {
        if (cacheEntries == null)
            throw new ArgumentException("DbCache.Load: empty cache entries", nameof(cacheEntries));
        if (_cache[GetCacheName(cacheName)] is not DbSingleCache<TKey, TValue> cache)
            throw new InvalidOperationException("DbCache.Load: invalid cache entries");
        cache.Load(cacheEntries);
    }

    /// <summary>
    /// return cache name from property expression
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="cacheName"></param>
    /// <returns></returns>
    static string GetCacheName(Expression<Func<IDbCacheName, string>> cacheName)
    {
        if (cacheName == null)
            throw new ArgumentException("DbCache.GetCacheName: cache name property expression is empty", nameof(cacheName));
        if (cacheName.Body is not MemberExpression propExpr)
            throw new InvalidOperationException("DbCache.GetCacheName: cache name property expression does not map to a known property");
        return propExpr.Member.Name;
    }

    private sealed class DbSingleCache<TKey, TValue> : IDbSingleCache
    {
        readonly System.Threading.Lock gate = new();
        Dictionary<TKey, TValue> entries = [];

        public void Clear()
        {
            lock (gate) entries.Clear();
        }

        public int Count()
        {
            lock (gate) return entries.Count;
        }

        public TValue Get(TKey key, Func<TValue>? getValue = null)
        {
            try
            {
                lock (gate)
                {
                    if (entries.TryGetValue(key, out var existing)) return existing;
                    if (getValue is null) return default!;
                    var value = getValue();
                    entries.Add(key, value);
                    return value;
                }
            }
            catch (Exception error)
            {
                throw new InvalidOperationException("DbCache.Get: cache key is invalid", error);
            }
        }

        public void Load(IDictionary<TKey, TValue> cacheEntries)
        {
            ArgumentNullException.ThrowIfNull(cacheEntries);
            lock (gate) entries = new Dictionary<TKey, TValue>(cacheEntries);
        }
    }
}
