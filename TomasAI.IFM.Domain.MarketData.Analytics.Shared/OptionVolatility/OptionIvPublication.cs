using System.Collections.Immutable;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

public sealed record OptionIvPublication(
    string Environment,
    OptionIvMetricRevision Metric,
    ImmutableArray<OptionIvObservation> SourceObservations);
