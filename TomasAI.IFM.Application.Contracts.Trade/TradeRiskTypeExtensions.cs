using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Shared;

public static class TradeRiskTypeExtensions
{

    extension(TradeRiskType value)
    {
        public string ToStringFast() => value switch
        {
            TradeRiskType.Unknown => nameof(TradeRiskType.Unknown),
            TradeRiskType.Low => nameof(TradeRiskType.Low),
            TradeRiskType.Medium => nameof(TradeRiskType.Medium),
            TradeRiskType.High => nameof(TradeRiskType.High),
            TradeRiskType.Critical => nameof(TradeRiskType.Critical),
            _ => value.ToString()
        };
    }
}
