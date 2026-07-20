using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Trade
{
    public enum OrderAction
    {
        Buy,
        Sell
    }

    public static class OrderActionExtensions
    {
        public static string ToStringFast(this OrderAction value) => value switch
        {
            OrderAction.Buy => nameof(OrderAction.Buy),
            OrderAction.Sell => nameof(OrderAction.Sell),
            _ => value.ToString()
        };
    }
}
