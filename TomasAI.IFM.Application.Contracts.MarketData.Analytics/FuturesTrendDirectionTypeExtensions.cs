using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared;

public static class FuturesTrendDirectionTypeExtensions
{

    extension(FuturesTrendDirectionType value)
    {
        public string ToStringFast() => value switch
        {
            FuturesTrendDirectionType.Init => nameof(FuturesTrendDirectionType.Init),
            FuturesTrendDirectionType.TrendReversal => nameof(FuturesTrendDirectionType.TrendReversal),
            FuturesTrendDirectionType.UpTrending => nameof(FuturesTrendDirectionType.UpTrending),
            FuturesTrendDirectionType.DownTrending => nameof(FuturesTrendDirectionType.DownTrending),
            FuturesTrendDirectionType.Flat => nameof(FuturesTrendDirectionType.Flat),
            _ => value.ToString()
        };
    }
}
