using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.MarketDataFeed
{
    public enum BarRateType
    {
         Minute
    }

    public static class BarRateTypeExtensions
    {
        public static string ToStringFast(this BarRateType value) => value switch
        {
            BarRateType.Minute => nameof(BarRateType.Minute),
            _ => value.ToString()
        };
    }
}
