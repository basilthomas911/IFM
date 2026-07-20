using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.MarketDataFeed
{
    public enum TickBidAskType
    {
        Bid,
        Ask
    }

    public static class TickBidAskTypeExtensions
    {
        public static string ToStringFast(this TickBidAskType value) => value switch
        {
            TickBidAskType.Bid => nameof(TickBidAskType.Bid),
            TickBidAskType.Ask => nameof(TickBidAskType.Ask),
            _ => value.ToString()
        };
    }
}
