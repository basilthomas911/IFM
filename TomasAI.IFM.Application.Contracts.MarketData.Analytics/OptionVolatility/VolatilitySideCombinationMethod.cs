using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

public enum VolatilitySideCombinationMethod
{
    Unspecified = 0,
    None = 1,
    ArithmeticMean = 2
}
