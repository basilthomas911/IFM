using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

public enum VolatilityHistoricalWindowConvention
{
    Unspecified = 0,
    PriorExchangeSessions = 1
}
