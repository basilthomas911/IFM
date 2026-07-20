using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Framework.Caching
{
    public interface IRedisCache
    {
        void Set(string key, string value);
        string Get(string key);
        void Remove(string key);
        Task SetAsync(string key, string value);
        Task<string> GetAsync(string key);
        Task RemoveAsync(string key);
    }
}
