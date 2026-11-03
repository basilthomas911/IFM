using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared
{

    public static class TradeStrateyTypeExtensions
    {

        extension(TradeStrateyType value)
        {
            public string ToStringFast() => value switch
            {
                TradeStrateyType.TrendFollowing => nameof(TradeStrateyType.TrendFollowing),
                TradeStrateyType.MeanReversion => nameof(TradeStrateyType.MeanReversion),
                TradeStrateyType.StatisticalArbitrage => nameof(TradeStrateyType.StatisticalArbitrage),
                _ => value.ToString()
            };
        }
    }
}
