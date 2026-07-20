using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed.Events;

namespace TomasAI.IFM.Service.AlgoTrader
{
    public interface IAlgoTraderService2
    {
        Task ExecuteAsync(IEvent e);
    }
}
