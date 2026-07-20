using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.MarketDataFeed;

namespace TomasAI.IFM.Service.MarketDataFeed.InteractiveBrokers
{
    public class TickOptionComputationMessage
    {
        private readonly int _requestId;
        private readonly TickOptionComputation _tickData;

        public int RequestId => _requestId;
        public TickOptionComputation TickOptionComputation => _tickData;

        public TickOptionComputationMessage(int requestId, TickOptionComputation tickData)
        {
            _requestId = requestId;
            _tickData = tickData;
        }
    }
}
