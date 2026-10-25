using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

public enum VolatilityMetricStatus
{
    Unspecified = 0,
    Qualified = 1,
    InsufficientHistory = 2,
    UndefinedRange = 3,
    InvalidCurrentObservation = 4
}
