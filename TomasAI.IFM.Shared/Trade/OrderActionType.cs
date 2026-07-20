using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Trade
{
    public enum OrderActionType
    {
        Open,
        Close,
    }

    public static class OrderActionTypeExtensions
    {
        public static string ToStringFast(this OrderActionType value) => value switch
        {
            OrderActionType.Open => nameof(OrderActionType.Open),
            OrderActionType.Close => nameof(OrderActionType.Close),
            _ => value.ToString()
        };
    }
}
