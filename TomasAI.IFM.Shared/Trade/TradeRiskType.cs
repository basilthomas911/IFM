using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Shared.Trade;

public enum TradeRiskType
{
    Unknown,
    Low,
    Medium,
    High,
    Critical
}

public static class TradeRiskTypeExtensions
{
    public static string ToStringFast(this TradeRiskType value) => value switch
    {
        TradeRiskType.Unknown => nameof(TradeRiskType.Unknown),
        TradeRiskType.Low => nameof(TradeRiskType.Low),
        TradeRiskType.Medium => nameof(TradeRiskType.Medium),
        TradeRiskType.High => nameof(TradeRiskType.High),
        TradeRiskType.Critical => nameof(TradeRiskType.Critical),
        _ => value.ToString()
    };
}
