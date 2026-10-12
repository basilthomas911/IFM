using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

public enum VolatilityFreshnessStatus
{
    Unspecified = 0,
    Accepted = 1,
    Stale = 2,
    Unavailable = 3
}
