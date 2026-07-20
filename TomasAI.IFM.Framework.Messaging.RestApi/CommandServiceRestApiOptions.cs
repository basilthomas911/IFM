using System;
using System.Collections.Generic;

namespace TomasAI.IFM.Framework.Messaging.RestApi
{
    /// <summary>
    /// command service rest api options
    /// </summary>
    public class CommandServiceRestApiOptions : ICommandServiceRestApiOptions
    {
        /// <summary>
        /// command service rest qpi options constructor
        /// </summary>
        public CommandServiceRestApiOptions(string baseUri)
        {
            if (string.IsNullOrWhiteSpace(baseUri))
                throw new ArgumentNullException(nameof(baseUri));
            BaseUri = baseUri;
        }

        /// <summary>
        /// return command service base uri
        /// </summary>
        public string BaseUri { get; }
    }
}
