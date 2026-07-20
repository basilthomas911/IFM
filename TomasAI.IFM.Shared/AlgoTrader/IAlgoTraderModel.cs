using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.AlgoTrader
{
    public interface IAlgoTraderModel<TEvent> where TEvent:IEvent
    {
    }
}
