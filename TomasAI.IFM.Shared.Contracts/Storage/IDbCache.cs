using System;
using System.Collections.Generic;
using System.Text;
using System.Linq.Expressions;

namespace TomasAI.IFM.Shared.Storage
{

    public interface IDbCache
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Preserve the existing public virtual/interface member and named argument contract used by actor implementations.")]
        TValue Get<TKey, TValue>(Expression<Func<IDbCacheName, string>> cacheName, TKey key, Func<TValue> getValue = null);
        void Clear(Expression<Func<IDbCacheName, string>> cacheName);
        int Count(Expression<Func<IDbCacheName, string>> cacheName);
        void Load<TKey, TValue>(Expression<Func<IDbCacheName, string>> cacheName, IDictionary<TKey, TValue> cacheEntries);
    }

}
