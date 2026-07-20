using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.MarketDataAnalytics
{
    public enum FuturesTrendDirectionStrengthType
    {
        Low,
        Medium,
        High
    }

    public static class FuturesTrendDirectionStrengthTypeExtensions
    {
        public static string ToStringFast(this FuturesTrendDirectionStrengthType value) => value switch
        {
            FuturesTrendDirectionStrengthType.Low => nameof(FuturesTrendDirectionStrengthType.Low),
            FuturesTrendDirectionStrengthType.Medium => nameof(FuturesTrendDirectionStrengthType.Medium),
            FuturesTrendDirectionStrengthType.High => nameof(FuturesTrendDirectionStrengthType.High),
            _ => value.ToString()
        };
    }
}
