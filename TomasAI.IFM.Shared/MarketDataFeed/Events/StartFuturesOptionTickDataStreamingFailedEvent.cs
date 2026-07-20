using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataFeed.Events
{
    public record StartFuturesOptionTickDataStreamingFailedEvent : ErrorEvent
    {
    }
}
