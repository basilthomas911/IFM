using System.Collections.Immutable;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

public sealed record VolatilityStorageScope(
    string Environment,
    VolatilitySeriesIdentity Series,
    string MetricPolicyVersion);
