using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.StatusConsole.SignalRClient
{
    public class StatusConsoleListenerOptions : IServiceApiListenerOptions
    {
        private readonly string _baseUri;

        public StatusConsoleListenerOptions(string baseUri)
            => _baseUri = baseUri;

        public string BaseUri => _baseUri;
    }
}
