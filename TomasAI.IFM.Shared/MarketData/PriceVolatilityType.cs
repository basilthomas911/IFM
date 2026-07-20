using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.MarketData
{
    public enum PriceVolatilityType
    {
        Unknown,
        Falling,
        Flat,
        Rising
    }

    public static class PriceVolatilityTypeExtensions
    {
        public static string ToStringFast(this PriceVolatilityType value) => value switch
        {
            PriceVolatilityType.Unknown => nameof(PriceVolatilityType.Unknown),
            PriceVolatilityType.Falling => nameof(PriceVolatilityType.Falling),
            PriceVolatilityType.Flat => nameof(PriceVolatilityType.Flat),
            PriceVolatilityType.Rising => nameof(PriceVolatilityType.Rising),
            _ => value.ToString()
        };
    }
}
