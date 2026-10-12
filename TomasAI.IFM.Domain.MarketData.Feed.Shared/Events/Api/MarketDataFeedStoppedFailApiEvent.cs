using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.Events.Api
{

    public record MarketDataFeedStoppedFailApiEvent : ErrorEvent
    {
    }
}
