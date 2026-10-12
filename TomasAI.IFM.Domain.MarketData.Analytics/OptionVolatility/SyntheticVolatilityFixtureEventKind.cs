using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.MarketData.Analytics.OptionVolatility;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "The fixture event protocol explicitly uses byte values; preserve its existing representation.")]
public enum SyntheticVolatilityFixtureEventKind : byte
{
    None = 0,
    Observation = 1,
    FuturesRoll = 2,
    Correction = 3
}
