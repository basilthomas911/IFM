using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Shared.Exceptions
{
    public interface IMarketDataExceptionEventProducer : IEventProducer
    {
    }
}
