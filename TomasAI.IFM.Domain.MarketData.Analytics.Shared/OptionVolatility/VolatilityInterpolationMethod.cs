using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

public enum VolatilityInterpolationMethod
{
    Unspecified = 0,
    None = 1,
    LinearVolatility = 2,
    LinearTotalVariance = 3
}
