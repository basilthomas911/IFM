using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared;

public static class FuturesAtrSignalSourceTypeExtensions
{
    public static string ToStringFast(this FuturesAtrSignalSourceType value) => value switch
    {
        FuturesAtrSignalSourceType.None => nameof(FuturesAtrSignalSourceType.None),
        FuturesAtrSignalSourceType.FuturesItiSignal => nameof(FuturesAtrSignalSourceType.FuturesItiSignal),
        FuturesAtrSignalSourceType.FuturesIntraDayData => nameof(FuturesAtrSignalSourceType.FuturesIntraDayData),
        _ => value.ToString()
    };
}
