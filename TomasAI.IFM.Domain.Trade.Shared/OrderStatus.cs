using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.Trade.Shared
{
    public enum OrderStatus
    {
        Placed,
        WaitingForFills,
        PartiallyFilled,
        Filled,
        Cancelled,
        Executed,
        Error
    }
}
