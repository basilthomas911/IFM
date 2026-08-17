using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared
{
    public enum BarRateType
    {
         Minute,
         FifteenSeconds
    }

    public static class BarRateTypeExtensions
    {
        public static string ToStringFast(this BarRateType value) => value switch
        {
            BarRateType.Minute => nameof(BarRateType.Minute),
            BarRateType.FifteenSeconds => nameof(BarRateType.FifteenSeconds),
            _ => value.ToString()
        };
    }
}
