using System;
using System.Collections.Generic;

namespace TomasAI.IFM.Framework.Messaging.RestApi
{
    /// <summary>
    /// query service rest api options
    /// </summary>
    public class QueryServiceRestApiOptions : IQueryServiceRestApiOptions
    {

        /// <summary>
        /// query service rest api constructor
        /// </summary>
        /// <param name="baseUri"></param>
        public QueryServiceRestApiOptions(string baseUri)
        {
            if (string.IsNullOrWhiteSpace(baseUri))
                throw new ArgumentNullException(nameof(baseUri));
            BaseUri = baseUri;
        }

        /// <summary>
        /// query service base uri
        /// </summary>
        public string BaseUri { get; }
    }
}
