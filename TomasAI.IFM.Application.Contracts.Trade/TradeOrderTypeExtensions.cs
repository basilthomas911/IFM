using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.Trade.Shared
{

    public static class TradeOrderTypeExtensions
    {

        extension(TradeOrderType value)
        {
            public string ToStringFast() => value switch
            {
                TradeOrderType.OptionTrade => nameof(TradeOrderType.OptionTrade),
                TradeOrderType.SpreadTrade => nameof(TradeOrderType.SpreadTrade),
                TradeOrderType.FuturesTrade => nameof(TradeOrderType.FuturesTrade),
                TradeOrderType.EquityTrade => nameof(TradeOrderType.EquityTrade),
                _ => value.ToString()
            };
        }
    }
}
