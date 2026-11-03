using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.Trade.Shared
{

    public static class OrderActionExtensions
    {

        extension(OrderAction value)
        {
            public string ToStringFast() => value switch
            {
                OrderAction.Buy => nameof(OrderAction.Buy),
                OrderAction.Sell => nameof(OrderAction.Sell),
                _ => value.ToString()
            };
        }
    }
}
