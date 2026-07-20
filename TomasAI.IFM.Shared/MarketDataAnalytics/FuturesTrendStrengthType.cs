using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.MarketDataAnalytics
{
    public enum FuturesTrendStrengthType
    {
        None,
        Low,
        Medium,
        High
    }

    public static class FuturesTrendStrengthTypeExtensions
    {
        public static string ToStringFast(this FuturesTrendStrengthType value) => value switch
        {
            FuturesTrendStrengthType.None => nameof(FuturesTrendStrengthType.None),
            FuturesTrendStrengthType.Low => nameof(FuturesTrendStrengthType.Low),
            FuturesTrendStrengthType.Medium => nameof(FuturesTrendStrengthType.Medium),
            FuturesTrendStrengthType.High => nameof(FuturesTrendStrengthType.High),
            _ => value.ToString()
        };
    }
}
