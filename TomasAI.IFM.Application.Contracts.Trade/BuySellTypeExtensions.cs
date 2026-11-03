using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.Trade.Shared
{

    public static class BuySellTypeExtensions
    {

        extension(BuySellType value)
        {
            public string ToStringFast() => value switch
            {
                BuySellType.Undefined => nameof(BuySellType.Undefined),
                BuySellType.Buy => nameof(BuySellType.Buy),
                BuySellType.Sell => nameof(BuySellType.Sell),
                BuySellType.Short => nameof(BuySellType.Short),
                BuySellType.Long => nameof(BuySellType.Long),
                _ => value.ToString()
            };
        }
    }
}
