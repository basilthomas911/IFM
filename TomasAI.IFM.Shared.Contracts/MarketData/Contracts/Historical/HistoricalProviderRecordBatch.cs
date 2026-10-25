using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;

namespace TomasAI.IFM.Shared.MarketData.Contracts.Historical;

/// <summary>Provides one bounded decoded historical batch.</summary>
/// <param name="Records">The records.</param>
/// <param name="BatchOrdinal">The batch ordinal.</param>
/// <param name="SourcePosition">The source position.</param>
/// <param name="IsFinal">The is final.</param>
public sealed record HistoricalProviderRecordBatch(
    IReadOnlyList<HistoricalProviderRecord> Records,
    long BatchOrdinal,
    string SourcePosition,
    bool IsFinal);
