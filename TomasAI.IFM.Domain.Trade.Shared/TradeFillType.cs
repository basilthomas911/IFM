using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.Trade.Shared
{
    public enum TradeFillType
    {
        Manual,
        Broker
    }

    public static class TradeFillTypeExtensions
    {
        public static string ToStringFast(this TradeFillType value) => value switch
        {
            TradeFillType.Manual => nameof(TradeFillType.Manual),
            TradeFillType.Broker => nameof(TradeFillType.Broker),
            _ => value.ToString()
        };
    }
}
