using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed.Events;

namespace TomasAI.IFM.Service.MarketDataAnalytics
{
    public interface IMarketDataAnalyticsService
    {
        Task ExecuteAsync(IEvent e);
    }
}
