using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared
{

    public static class TradeSignalTypeExtensions
    {

        extension(TradeSignalType value)
        {
            public string ToStringFast() => value switch
            {
                TradeSignalType.None => nameof(TradeSignalType.None),
                TradeSignalType.Buy => nameof(TradeSignalType.Buy),
                TradeSignalType.Sell => nameof(TradeSignalType.Sell),
                _ => value.ToString()
            };
        }
    }
}
