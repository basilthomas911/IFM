using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.Trade.Shared
{
    public enum TradeStrategy
    {
        Unknown,
        IntrinsicTimeLongShort
    }

    public static class TradeStrategyExtensions
    {
        public static string ToStringFast(this TradeStrategy value) => value switch
        {
            TradeStrategy.Unknown => nameof(TradeStrategy.Unknown),
            TradeStrategy.IntrinsicTimeLongShort => nameof(TradeStrategy.IntrinsicTimeLongShort),
            _ => value.ToString()
        };
    }
}
