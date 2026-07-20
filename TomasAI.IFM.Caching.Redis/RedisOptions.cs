using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Framework.Caching.Redis
{
    /// <summary>
    /// redis client settings
    /// </summary>
    public class RedisOptions : IRedisOptions
    {
        public RedisOptions(string baseUri)
        {
            if (string.IsNullOrEmpty(baseUri))
                throw new ArgumentNullException(nameof(baseUri));
            BaseUri = baseUri;
        }

        public string BaseUri { get; }

    }
}
