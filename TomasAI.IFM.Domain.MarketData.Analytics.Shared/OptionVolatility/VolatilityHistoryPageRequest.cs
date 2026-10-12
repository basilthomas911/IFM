using System.Collections.Immutable;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public model contract.")]
public sealed record VolatilityHistoryPageRequest(
    VolatilityStorageScope Scope,
    VolatilityCalendarBucket Bucket,
    DateOnly FromValueDate,
    DateOnly ToValueDate,
    string? SamplingSlot,
    VolatilityHistoricalMode Mode,
    DateTimeOffset? KnownAtUtc,
    int PageSize,
    byte[]? PagingState);
