using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared
{
    public enum TradeSignalType
    {
        None,
        Buy,
        Sell
    }

    public static class TradeSignalTypeExtensions
    {
        public static string ToStringFast(this TradeSignalType value) => value switch
        {
            TradeSignalType.None => nameof(TradeSignalType.None),
            TradeSignalType.Buy => nameof(TradeSignalType.Buy),
            TradeSignalType.Sell => nameof(TradeSignalType.Sell),
            _ => value.ToString()
        };
    }
}
