using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Domain.Trade.Shared.TradeOrder;

public enum TradeOrderState
{
    OrderPlaced,
    OrderOpened,
    OrderPartiallyFilled,
    OrderFilled,
    OrderClosed,
    OrderCancelled,
    OrderFailed
}
