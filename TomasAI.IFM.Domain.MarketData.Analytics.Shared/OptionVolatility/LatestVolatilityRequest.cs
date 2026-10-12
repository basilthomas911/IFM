using System.Collections.Immutable;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

public sealed record LatestVolatilityRequest(
    VolatilityStorageScope Scope,
    DateTimeOffset RequestedAtUtc,
    TimeSpan MaximumAge);
