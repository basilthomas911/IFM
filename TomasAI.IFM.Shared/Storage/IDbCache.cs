using System;
using System.Collections.Generic;
using System.Text;
using System.Linq.Expressions;

namespace TomasAI.IFM.Shared.Storage
{
    public interface IDbCacheName
    {
        string EntityTypeIdMap { get; }
        string EventTypeIdMap { get; }
        string FuturesEodDataMap { get; }
        string FutureOptionTickDataMap { get; }
    }

    public interface IDbCache
    {
        TValue Get<TKey, TValue>(Expression<Func<IDbCacheName, string>> cacheName, TKey key, Func<TValue> getValue = null);
        void Clear(Expression<Func<IDbCacheName, string>> cacheName);
        int Count(Expression<Func<IDbCacheName, string>> cacheName);
        void Load<TKey, TValue>(Expression<Func<IDbCacheName, string>> cacheName, IDictionary<TKey, TValue> cacheEntries);
    }

    public interface IDbSingleCache
    { 
    }

}
