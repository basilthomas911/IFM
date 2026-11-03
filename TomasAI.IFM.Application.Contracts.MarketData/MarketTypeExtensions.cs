using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Domain.MarketData.Shared
{

    public static class MarketTypeExtensions
    {

        extension(MarketType value)
        {
            public string ToStringFast() => value switch
            {
                MarketType.Equity => nameof(MarketType.Equity),
                MarketType.Futures => nameof(MarketType.Futures),
                _ => value.ToString()
            };
        }
    }
}
