using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

public enum VolatilityGapPolicy
{
    Unspecified = 0,
    PreserveExpectedSessionGap = 1
}
