using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.Events;

namespace TomasAI.IFM.TradePositionFeed.SignalRClient
{
    public interface ITradePositionFeedListener 
    {
        Task StartAsync(Action<TradePositionUpdatedEvent> eventAction);
        Task StopAsync();
        Task ResetAsync();
    }
}
