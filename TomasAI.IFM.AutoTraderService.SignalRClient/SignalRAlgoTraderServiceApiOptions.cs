using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.AlgoTrader.SignalRClient
{
    public class SignalRAlgoTraderServiceApiOptions : IAlgoTraderServiceApiOptions
    {
        private readonly string _baseUri;

        public SignalRAlgoTraderServiceApiOptions(string baseUri)
            => _baseUri = baseUri;

        public string BaseUri => _baseUri;
    }
}
