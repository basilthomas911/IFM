using System.Collections.Immutable;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

public sealed record VolatilitySeriesConstructionRequest(
    VolatilitySeriesDefinition Definition,
    DateOnly ExchangeValueDate,
    string SamplingSlot,
    DateTimeOffset AsOfUtc,
    int Revision,
    string? SupersedesObservationId,
    ImmutableArray<QualifiedOptionIvInput> Inputs);
