using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.AlgoTrader
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1040", Justification = "The marker interface preserves the existing actor/model routing contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2326", Justification = "The generic parameter is the existing exact typed actor message dispatch contract.")]
    public interface IAlgoTraderModel<TEvent> where TEvent : IEvent
    {
    }
}
