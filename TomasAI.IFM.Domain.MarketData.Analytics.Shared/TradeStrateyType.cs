using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared
{
    public enum TradeStrateyType
    {
        TrendFollowing,
        MeanReversion,
        StatisticalArbitrage
    }

    public static class TradeStrateyTypeExtensions
    {
        public static string ToStringFast(this TradeStrateyType value) => value switch
        {
            TradeStrateyType.TrendFollowing => nameof(TradeStrateyType.TrendFollowing),
            TradeStrateyType.MeanReversion => nameof(TradeStrateyType.MeanReversion),
            TradeStrateyType.StatisticalArbitrage => nameof(TradeStrateyType.StatisticalArbitrage),
            _ => value.ToString()
        };
    }
}
