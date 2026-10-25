using System.Collections.Immutable;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

public sealed record VolatilitySeriesConstructionResult(
    OptionIvObservation Observation,
    VolatilitySeriesConstructionFailure Failure,
    ImmutableArray<string> DiagnosticCodes);
