using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared
{

    public static class BarRateTypeExtensions
    {

        extension(BarRateType value)
        {
            public string ToStringFast() => value switch
            {
                BarRateType.Minute => nameof(BarRateType.Minute),
                BarRateType.FifteenSeconds => nameof(BarRateType.FifteenSeconds),
                _ => value.ToString()
            };
        }
    }
}
