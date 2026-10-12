using System.Collections.Immutable;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

public sealed record LatestVolatilityResult(
    OptionIvMetricRevision? Metric,
    VolatilityFreshnessStatus FreshnessStatus);
