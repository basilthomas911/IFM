using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.Trade.Shared
{
    public enum OrderType
    {
        Limit,
        Market,
        CloseAlgo
    }

    public static class OrderTypeExtensions
    {
        public static string ToStringFast(this OrderType value) => value switch
        {
            OrderType.Limit => nameof(OrderType.Limit),
            OrderType.Market => nameof(OrderType.Market),
            OrderType.CloseAlgo => nameof(OrderType.CloseAlgo),
            _ => value.ToString()
        };
    }
}
