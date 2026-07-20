using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed.Events;

namespace TomasAI.IFM.Application.MarketData
{
    public interface IMarketDataService
    {
        Task ExecuteAsync(IEvent e);
    }
}
