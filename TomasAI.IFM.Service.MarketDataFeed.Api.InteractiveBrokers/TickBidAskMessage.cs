using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.MarketDataFeed;

namespace TomasAI.IFM.Service.MarketDataFeed.Api.InteractiveBrokers
{
    public class TickBidAskMessage
    {
        private readonly int _requestId;
        private readonly TickBidAskData _tickData;

        public int RequestId => _requestId;
        public TickBidAskData TickBidAskData => _tickData;

        public TickBidAskMessage(int requestId, TickBidAskData tickData)
        {
            _requestId = requestId;
            _tickData = tickData;
        }
    }
}
