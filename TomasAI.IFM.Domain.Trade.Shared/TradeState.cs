using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Domain.Trade.Shared;

public enum TradeState
{
    NewTrade,
    OrderPlaced,
    OrderPartiallyFilled,
    OrderFilled,
    OrderCancelled,
    TradeToOpen,
    TradeToClose,
    OrderCompleted,
    OrderSubmitted,
    Open,
    Closed
}
