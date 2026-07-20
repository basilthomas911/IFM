using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Trade
{
    public enum ActionSource
    {
        TradePosition,
        AlgoTrader,
        OrderExecution
    }

    public static class ActionSourceExtensions
    {
        public static string ToStringFast(this ActionSource value) => value switch
        {
            ActionSource.TradePosition => nameof(ActionSource.TradePosition),
            ActionSource.AlgoTrader => nameof(ActionSource.AlgoTrader),
            ActionSource.OrderExecution => nameof(ActionSource.OrderExecution),
            _ => value.ToString()
        };
    }
}
