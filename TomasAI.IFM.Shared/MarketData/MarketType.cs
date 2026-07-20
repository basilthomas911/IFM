using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.MarketData
{
    public enum MarketType
    {
        Equity,
        Futures
    }

    public static class MarketTypeExtensions
    {
        public static string ToStringFast(this MarketType value) => value switch
        {
            MarketType.Equity => nameof(MarketType.Equity),
            MarketType.Futures => nameof(MarketType.Futures),
            _ => value.ToString()
        };
    }
}
