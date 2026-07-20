using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Trade
{
    public enum TradePlacementSignalType
    {
        Cleared,
        Wait,
        Set,
        Rangebound
    }

    public static class TradePlacementSignalTypeExtensions
    {
        public static string ToStringFast(this TradePlacementSignalType value) => value switch
        {
            TradePlacementSignalType.Cleared => nameof(TradePlacementSignalType.Cleared),
            TradePlacementSignalType.Wait => nameof(TradePlacementSignalType.Wait),
            TradePlacementSignalType.Set => nameof(TradePlacementSignalType.Set),
            TradePlacementSignalType.Rangebound => nameof(TradePlacementSignalType.Rangebound),
            _ => value.ToString()
        };
    }
}
