using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.MarketData
{
    public enum PriceDirectionType
    {
        Rising,
        RisingSlowly,
        Flat,
        FallingSlowly,
        Falling
    }

    public static class PriceDirectionTypeExtensions
    {
        public static string ToStringFast(this PriceDirectionType value) => value switch
        {
            PriceDirectionType.Rising => nameof(PriceDirectionType.Rising),
            PriceDirectionType.RisingSlowly => nameof(PriceDirectionType.RisingSlowly),
            PriceDirectionType.Flat => nameof(PriceDirectionType.Flat),
            PriceDirectionType.FallingSlowly => nameof(PriceDirectionType.FallingSlowly),
            PriceDirectionType.Falling => nameof(PriceDirectionType.Falling),
            _ => value.ToString()
        };
    }
}
