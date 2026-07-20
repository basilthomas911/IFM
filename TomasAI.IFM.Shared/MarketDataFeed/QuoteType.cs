using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.MarketDataFeed
{
    public enum QuoteType
    {
        Price,
        Size
    }

    public static class QuoteTypeExtensions
    {
        public static string ToStringFast(this QuoteType value) => value switch
        {
            QuoteType.Price => nameof(QuoteType.Price),
            QuoteType.Size => nameof(QuoteType.Size),
            _ => value.ToString()
        };
    }
}
