using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed.Events;

namespace TomasAI.IFM.Service.MarketDataFeed
{
    public interface IMarketDataFeedService
    {
        Task ExecuteAsync(IEvent e);
    }
}
