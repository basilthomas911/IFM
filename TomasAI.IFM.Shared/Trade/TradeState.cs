using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.Trade;

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
    OrderSubmitted
}

public static class TradeStateExtensions
{
    public static string ToStringFast(this TradeState value) => value switch
    {
        TradeState.NewTrade => nameof(TradeState.NewTrade),
        TradeState.OrderPlaced => nameof(TradeState.OrderPlaced),
        TradeState.OrderPartiallyFilled => nameof(TradeState.OrderPartiallyFilled),
        TradeState.OrderFilled => nameof(TradeState.OrderFilled),
        TradeState.OrderCancelled => nameof(TradeState.OrderCancelled),
        TradeState.TradeToOpen => nameof(TradeState.TradeToOpen),
        TradeState.TradeToClose => nameof(TradeState.TradeToClose),
        TradeState.OrderCompleted => nameof(TradeState.OrderCompleted),
        TradeState.OrderSubmitted => nameof(TradeState.OrderSubmitted),
        _ => value.ToString()
    };
}
