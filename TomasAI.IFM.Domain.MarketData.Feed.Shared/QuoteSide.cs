using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared
{
    public enum QuoteSide
    {
        Undefined,
        Ask,
        Bid
    }

    public static class QuoteSideExtensions
    {
        public static string ToStringFast(this QuoteSide value) => value switch
        {
            QuoteSide.Undefined => nameof(QuoteSide.Undefined),
            QuoteSide.Ask => nameof(QuoteSide.Ask),
            QuoteSide.Bid => nameof(QuoteSide.Bid),
            _ => value.ToString()
        };
    }
}
