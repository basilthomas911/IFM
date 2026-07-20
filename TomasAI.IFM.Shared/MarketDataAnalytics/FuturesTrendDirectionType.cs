using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.MarketDataAnalytics;

public enum FuturesTrendDirectionType
{
    Init,
    TrendReversal,
    UpTrending,
    DownTrending,
    Flat
}

public static class FuturesTrendDirectionTypeExtensions
{
    public static string ToStringFast(this FuturesTrendDirectionType value) => value switch
    {
        FuturesTrendDirectionType.Init => nameof(FuturesTrendDirectionType.Init),
        FuturesTrendDirectionType.TrendReversal => nameof(FuturesTrendDirectionType.TrendReversal),
        FuturesTrendDirectionType.UpTrending => nameof(FuturesTrendDirectionType.UpTrending),
        FuturesTrendDirectionType.DownTrending => nameof(FuturesTrendDirectionType.DownTrending),
        FuturesTrendDirectionType.Flat => nameof(FuturesTrendDirectionType.Flat),
        _ => value.ToString()
    };
}
