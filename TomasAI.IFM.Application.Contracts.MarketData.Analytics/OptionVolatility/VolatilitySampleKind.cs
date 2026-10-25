using System.Collections.Immutable;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1008", Justification = "Zero is intentionally invalid for this existing serialized enum; preserve its existing values.")]
public enum VolatilitySampleKind
{
    DailyFinal = 1,
    IntradayCheckpoint = 2
}
