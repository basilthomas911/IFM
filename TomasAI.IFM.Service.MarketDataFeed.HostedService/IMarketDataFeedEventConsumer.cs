using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Service.MarketDataFeed.HostedService
{
    public interface IMarketDataFeedEventConsumer : IEventConsumer
    {
    }
}
