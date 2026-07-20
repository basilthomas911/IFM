using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataFeed.Events
{
    public record InsertFuturesEodDataFailedEvent : ErrorEvent
    {
    }
}
