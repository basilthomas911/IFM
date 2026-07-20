using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.AlgoTrader.SignalRClient
{
    public class SignalRTradePlanServiceApiOptions : ITradePlanServiceApiOptions
    {
        private readonly string _baseUri;

        public string BaseUri => _baseUri;

        public SignalRTradePlanServiceApiOptions(string baseUri)
            => _baseUri = baseUri;
    }
}
