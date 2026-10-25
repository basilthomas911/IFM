using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Shared.AlgoTrader
{
    public interface ITradePlanUpdatedEvent : IEvent
    {
        TradePlanReadModel TradePlan { get; init; }

        ICompleteEvent ToCompletedEvent();

        IErrorEvent ToFailedEvent(Exception ex);
    }
}
