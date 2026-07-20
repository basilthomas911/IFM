using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.TradeOrder;

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

public static class TradeOrderStateExtensions
{
    public static string ToStringFast(this TradeOrderState value) => value switch
    {
        TradeOrderState.OrderPlaced => nameof(TradeOrderState.OrderPlaced),
        TradeOrderState.OrderOpened => nameof(TradeOrderState.OrderOpened),
        TradeOrderState.OrderPartiallyFilled => nameof(TradeOrderState.OrderPartiallyFilled),
        TradeOrderState.OrderFilled => nameof(TradeOrderState.OrderFilled),
        TradeOrderState.OrderClosed => nameof(TradeOrderState.OrderClosed),
        TradeOrderState.OrderCancelled => nameof(TradeOrderState.OrderCancelled),
        TradeOrderState.OrderFailed => nameof(TradeOrderState.OrderFailed),
        _ => value.ToString()
    };
}
