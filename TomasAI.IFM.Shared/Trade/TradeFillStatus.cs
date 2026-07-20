using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Trade
{
    public enum TradeFillStatus
    {
        Empty,
        PartiallyFilled,
        Filled
    }

    public static class TradeFillStatusExtensions
    {
        public static string ToStringFast(this TradeFillStatus value) => value switch
        {
            TradeFillStatus.Empty => nameof(TradeFillStatus.Empty),
            TradeFillStatus.PartiallyFilled => nameof(TradeFillStatus.PartiallyFilled),
            TradeFillStatus.Filled => nameof(TradeFillStatus.Filled),
            _ => value.ToString()
        };
    }
}
