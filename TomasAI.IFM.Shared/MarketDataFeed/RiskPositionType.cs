using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.MarketDataFeed;

public enum RiskPositionType
{
    Unknown,
    Low,
    Medium,
    High,
}

public static class RiskPositionTypeExtensions
{
    public static string ToStringFast(this RiskPositionType value) => value switch
    {
        RiskPositionType.Unknown => nameof(RiskPositionType.Unknown),
        RiskPositionType.Low => nameof(RiskPositionType.Low),
        RiskPositionType.Medium => nameof(RiskPositionType.Medium),
        RiskPositionType.High => nameof(RiskPositionType.High),
        _ => value.ToString()
    };
}
