using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

public enum VolatilityMoneynessConvention
{
    Unspecified = 0,
    AtTheMoneyForward = 1,
    AtTheMoneySpot = 2,
    DeltaBased = 3
}
