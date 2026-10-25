using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

[MessagePackObject]
public sealed record VolatilityQualificationPolicy(
    [property: Key(0)] string QuoteAndIvMarkBasis,
    [property: Key(1)] string LiquidityAndQualityRulesVersion,
    [property: Key(2)] bool RequireExactUnderlyingMatch,
    [property: Key(3)] TimeSpan MaximumQuoteAge,
    [property: Key(4)] TimeSpan MaximumIvAge,
    [property: Key(5)] TimeSpan MaximumQuoteSkew);
