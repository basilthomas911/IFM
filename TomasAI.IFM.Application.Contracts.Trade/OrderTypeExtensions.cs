using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.Trade.Shared
{

    public static class OrderTypeExtensions
    {

        extension(OrderType value)
        {
            public string ToStringFast() => value switch
            {
                OrderType.Limit => nameof(OrderType.Limit),
                OrderType.Market => nameof(OrderType.Market),
                OrderType.CloseAlgo => nameof(OrderType.CloseAlgo),
                _ => value.ToString()
            };
        }
    }
}
