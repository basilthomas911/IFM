using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.Trade.Shared
{

    public static class TradeFillStatusExtensions
    {

        extension(TradeFillStatus value)
        {
            public string ToStringFast() => value switch
            {
                TradeFillStatus.Empty => nameof(TradeFillStatus.Empty),
                TradeFillStatus.PartiallyFilled => nameof(TradeFillStatus.PartiallyFilled),
                TradeFillStatus.Filled => nameof(TradeFillStatus.Filled),
                _ => value.ToString()
            };
        }
    }
}
