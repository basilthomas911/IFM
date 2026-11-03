using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared
{

    public static class QuoteTypeExtensions
    {

        extension(QuoteType value)
        {
            public string ToStringFast() => value switch
            {
                QuoteType.Price => nameof(QuoteType.Price),
                QuoteType.Size => nameof(QuoteType.Size),
                _ => value.ToString()
            };
        }
    }
}
