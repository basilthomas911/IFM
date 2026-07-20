using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.StatusConsole.SignalRClient
{
    public class StatusConsoleServiceApiOptions : IStatusConsoleServiceApiOptions
    {
        private readonly string _baseUri;

        public string BaseUri => _baseUri;

        public StatusConsoleServiceApiOptions(string baseUri)
            => _baseUri = baseUri;
    }
}
