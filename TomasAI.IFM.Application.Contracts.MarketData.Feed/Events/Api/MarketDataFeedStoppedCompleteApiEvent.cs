using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.Events.Api
{

    public record MarketDataFeedStoppedCompleteApiEvent : CompleteEvent
    {
        public DateTime StoppedOn { get; init; }
        public string StoppedBy { get; init; }
    }
}
