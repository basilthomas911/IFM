using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

public enum VolatilityDependencyRequirement
{
    Unspecified = 0,
    Optional = 1,
    Required = 2
}
