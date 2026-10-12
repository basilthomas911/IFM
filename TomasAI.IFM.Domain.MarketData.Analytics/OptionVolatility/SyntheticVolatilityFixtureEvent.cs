using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.MarketData.Analytics.OptionVolatility;

public sealed record SyntheticVolatilityFixtureEvent(
    SyntheticVolatilityFixtureEventKind Kind,
    DateTimeOffset OccurredAtUtc,
    string Description,
    string? ObservationId);
