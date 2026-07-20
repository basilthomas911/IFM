using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.MarketDataFeed;

namespace TomasAI.IFM.Service.MarketDataFeed.Api.InteractiveBrokers
{
    public class TickPriceMessage
    {
        private readonly int _requestId;
        private readonly TickPriceData _tickData;

        public int RequestId => _requestId;
        public TickPriceData TickPriceData => _tickData;

        public TickPriceMessage(int requestId, TickPriceData tickData)
        {
            _requestId = requestId;
            _tickData = tickData;
        }
    }
}
