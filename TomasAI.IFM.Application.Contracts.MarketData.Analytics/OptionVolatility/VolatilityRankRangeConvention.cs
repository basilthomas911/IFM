using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

public enum VolatilityRankRangeConvention
{
    Unspecified = 0,
    CurrentAndPriorWindow = 1
}
