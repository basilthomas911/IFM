using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.MarketDataFeed;

namespace TomasAI.IFM.Service.MarketDataFeed.InteractiveBrokers
{
    public class TickPriceMessage
    {
  
        public TickPriceMessage(int requestId, TickPriceData tickData)
        {
            RequestId = requestId;
            TickPriceData = tickData;
        }

        public int RequestId { get; }
        public TickPriceData TickPriceData { get; }

    }
}
