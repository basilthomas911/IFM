using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Trade
{
    public enum BuySellType
    {
        Undefined,
        Buy,
        Sell,
        Short,
        Long
    }

    public static class BuySellTypeExtensions
    {
        public static string ToStringFast(this BuySellType value) => value switch
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
