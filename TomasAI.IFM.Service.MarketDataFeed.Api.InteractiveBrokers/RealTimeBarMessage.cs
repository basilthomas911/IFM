using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.MarketDataFeed;

namespace TomasAI.IFM.Service.MarketDataFeed.Api.InteractiveBrokers
{
    public class RealTimeBarMessage
    {
        private readonly int _requestId;
        private readonly RealTimeBarData _rtbData;

        public int RequestId => _requestId;
        public RealTimeBarData RealTimeBarData => _rtbData;

        public RealTimeBarMessage(int requestId, RealTimeBarData rtbData)
        {
            _requestId = requestId;
            _rtbData = rtbData;
        }

    }
}
