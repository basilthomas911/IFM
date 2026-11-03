using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared
{

    public static class TickBidAskTypeExtensions
    {

        extension(TickBidAskType value)
        {
            public string ToStringFast() => value switch
            {
                TickBidAskType.Bid => nameof(TickBidAskType.Bid),
                TickBidAskType.Ask => nameof(TickBidAskType.Ask),
                _ => value.ToString()
            };
        }
    }
}
