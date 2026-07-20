using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Trade
{
    public enum TradeOrderType
    {
        OptionTrade,
        SpreadTrade,
        FuturesTrade,
        EquityTrade
    }

    public static class TradeOrderTypeExtensions
    {
        public static string ToStringFast(this TradeOrderType value) => value switch
        {
            TradeOrderType.OptionTrade => nameof(TradeOrderType.OptionTrade),
            TradeOrderType.SpreadTrade => nameof(TradeOrderType.SpreadTrade),
            TradeOrderType.FuturesTrade => nameof(TradeOrderType.FuturesTrade),
            TradeOrderType.EquityTrade => nameof(TradeOrderType.EquityTrade),
            _ => value.ToString()
        };
    }
}
