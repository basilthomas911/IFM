using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared
{

    public static class FuturesTrendStrengthTypeExtensions
    {

        extension(FuturesTrendStrengthType value)
        {
            public string ToStringFast() => value switch
            {
                FuturesTrendStrengthType.None => nameof(FuturesTrendStrengthType.None),
                FuturesTrendStrengthType.Low => nameof(FuturesTrendStrengthType.Low),
                FuturesTrendStrengthType.Medium => nameof(FuturesTrendStrengthType.Medium),
                FuturesTrendStrengthType.High => nameof(FuturesTrendStrengthType.High),
                _ => value.ToString()
            };
        }
    }
}
