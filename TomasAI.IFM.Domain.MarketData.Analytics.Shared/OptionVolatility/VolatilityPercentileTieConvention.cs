using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

public enum VolatilityPercentileTieConvention
{
    Unspecified = 0,
    StrictlyLessThanCurrent = 1
}
