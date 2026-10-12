using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared
{

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
