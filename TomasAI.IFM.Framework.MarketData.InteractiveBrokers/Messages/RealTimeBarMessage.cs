using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.MarketDataFeed;

namespace TomasAI.IFM.Framework.MarketData.InteractiveBrokers.Messages
{
    public record RealTimeBarMessage(
        int RequestId,
        RealTimeBarData RealTimeBarData)
    {
    }
}
