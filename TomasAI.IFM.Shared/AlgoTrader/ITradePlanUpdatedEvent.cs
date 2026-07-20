using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Shared.AlgoTrader
{
    public interface ITradePlanUpdatedEvent : IEvent
    {
        TradePlanReadModel TradePlan { get; init; }

        ICompleteEvent ToCompletedEvent();

        IErrorEvent ToFailedEvent(Exception ex);
    }
}
