using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.MarketData
{
    public enum MarketVolatilityType
    {
        Low,
        Normal,
        High,
        Rising,
        Falling
    }

    public static class MarketVolatilityTypeExtensions
    {
        public static string ToStringFast(this MarketVolatilityType value) => value switch
        {
            MarketVolatilityType.Low => nameof(MarketVolatilityType.Low),
            MarketVolatilityType.Normal => nameof(MarketVolatilityType.Normal),
            MarketVolatilityType.High => nameof(MarketVolatilityType.High),
            MarketVolatilityType.Rising => nameof(MarketVolatilityType.Rising),
            MarketVolatilityType.Falling => nameof(MarketVolatilityType.Falling),
            _ => value.ToString()
        };
    }
}
