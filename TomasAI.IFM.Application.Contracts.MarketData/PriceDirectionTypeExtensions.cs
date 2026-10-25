using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.MarketData.Shared
{

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
