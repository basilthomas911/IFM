using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.Trade.Shared
{

    public static class TradeStrategyExtensions
    {

        extension(TradeStrategy value)
        {
            public string ToStringFast() => value switch
            {
                TradeStrategy.Unknown => nameof(TradeStrategy.Unknown),
                TradeStrategy.IntrinsicTimeLongShort => nameof(TradeStrategy.IntrinsicTimeLongShort),
                _ => value.ToString()
            };
        }
    }
}
