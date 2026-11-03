using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.Trade.Shared
{

    public static class TradeSubTypeExtensions
    {

        extension(TradeSubType value)
        {
            public string ToStringFast() => value switch
            {
                TradeSubType.Primary => nameof(TradeSubType.Primary),
                TradeSubType.Hedge => nameof(TradeSubType.Hedge),
                _ => value.ToString()
            };
        }
    }
}
