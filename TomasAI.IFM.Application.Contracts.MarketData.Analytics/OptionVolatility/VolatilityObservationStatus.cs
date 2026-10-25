using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

public enum VolatilityObservationStatus
{
    Unspecified = 0,
    Qualified = 1,
    Partial = 2,
    Unavailable = 3,
    Invalid = 4,
    Incompatible = 5
}
