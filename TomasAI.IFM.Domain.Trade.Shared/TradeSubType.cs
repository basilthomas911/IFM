using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.Trade.Shared
{
    public enum TradeSubType
    {
        Primary,
        Hedge
    }

    public static class TradeSubTypeExtensions
    {
        public static string ToStringFast(this TradeSubType value) => value switch
        {
            TradeSubType.Primary => nameof(TradeSubType.Primary),
            TradeSubType.Hedge => nameof(TradeSubType.Hedge),
            _ => value.ToString()
        };
    }
}
