using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared
{

    public static class QuoteSideExtensions
    {

        extension(QuoteSide value)
        {
            public string ToStringFast() => value switch
            {
                QuoteSide.Undefined => nameof(QuoteSide.Undefined),
                QuoteSide.Ask => nameof(QuoteSide.Ask),
                QuoteSide.Bid => nameof(QuoteSide.Bid),
                _ => value.ToString()
            };
        }
    }
}
