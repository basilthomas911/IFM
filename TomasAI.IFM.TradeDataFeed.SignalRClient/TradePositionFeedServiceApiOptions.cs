using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.TradePositionFeed.SignalRClient
{
    public class TradePositionFeedServiceApiOptions : ITradePositionFeedServiceApiOptions
    {
        private readonly string _baseUri;

        public string BaseUri => _baseUri;

        public TradePositionFeedServiceApiOptions(string baseUri)
            => _baseUri = baseUri;
    }
}
