using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Caching
{
    public interface IDataCacheItem
    {
        bool IsEmpty();
        TValue GetValue<TValue>() where TValue:class;
        void SetValue<TValue>(TValue value) where TValue:class;
    }
}
