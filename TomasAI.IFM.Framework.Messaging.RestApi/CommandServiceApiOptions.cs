using System;
using System.Collections.Generic;

namespace TomasAI.IFM.Framework.Messaging.RestApi
{
    /// <summary>
    /// command service rest api options
    /// </summary>
    public class CommandServiceApiOptions : ICommandServiceApiOptions
    {
        /// <summary>
        /// command service rest qpi options constructor
        /// </summary>
        public CommandServiceApiOptions(string baseUri)
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
