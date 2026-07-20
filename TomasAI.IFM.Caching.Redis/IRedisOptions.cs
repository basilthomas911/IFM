using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Framework.Caching.Redis
{
    public interface IRedisOptions
    {
        string BaseUri { get; }
    }
}
