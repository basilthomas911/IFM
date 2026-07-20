using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.EventSourcing
{
    public class BoundedContextStateCache : IBoundedContextStateCache
    {
        private Dictionary<Type, Dictionary<long, object>> _cacheMap;

        public BoundedContextStateCache() => _cacheMap = new Dictionary<Type, Dictionary<long, object>>();

        public void Add<TState>(long key, TState value) where TState : class, IBoundedContextState
        {
            if (!_cacheMap.ContainsKey(typeof(TState)))
                _cacheMap.Add(typeof(TState), new Dictionary<long, object>());
            var cache = _cacheMap[typeof(TState)];
            cache.Add(key, value);
        }

        public bool Exists<TState>(long key) where TState : class, IBoundedContextState
        {
            if (_cacheMap.ContainsKey(typeof(TState))) return false;
            var cache = _cacheMap[typeof(TState)];
            return cache.ContainsKey(key);
        }

        public void Remove<TState>(long key) where TState : class, IBoundedContextState
        {
            if (_cacheMap.ContainsKey(typeof(TState)))
            {
                var cache = _cacheMap[typeof(TState)];
                cache.Remove(key);
            }
        }

        public TState Get<TState>(long key) where TState : class, IBoundedContextState
        {
            var state = default(TState);
            if (_cacheMap.ContainsKey(typeof(TState)))
            {
                var cache = _cacheMap[typeof(TState)];
                state = (TState)cache[key];
            }
            return state!;
        }

    }
}
