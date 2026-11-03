using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.Trade.Shared
{

    public static class ActionSourceExtensions
    {

        extension(ActionSource value)
        {
            public string ToStringFast() => value switch
            {
                ActionSource.TradePosition => nameof(ActionSource.TradePosition),
                ActionSource.AlgoTrader => nameof(ActionSource.AlgoTrader),
                ActionSource.OrderExecution => nameof(ActionSource.OrderExecution),
                _ => value.ToString()
            };
        }
    }
}
