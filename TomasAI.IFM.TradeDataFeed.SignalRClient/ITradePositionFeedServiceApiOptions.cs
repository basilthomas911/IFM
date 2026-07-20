using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.TradePositionFeed.SignalRClient
{
    public interface ITradePositionFeedServiceApiOptions
    {
        string BaseUri { get; }
    }
}
