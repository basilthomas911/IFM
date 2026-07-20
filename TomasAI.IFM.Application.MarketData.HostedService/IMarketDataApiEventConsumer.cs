using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.MarketData.HostedService
{
    public interface IMarketDataApiEventConsumer : IEventConsumer
    {
    }
}
