using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

public enum VolatilityOptionSideSelection
{
    Unspecified = 0,
    Calls = 1,
    Puts = 2,
    CallPutCombined = 3
}
