using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;

/// <summary>Represents EMA10/20/50/200 calculated from one shared close lineage.</summary>
[MessagePackObject]
public sealed record FuturesEmaSignalReadModel
{
    /// <summary>Gets common identity and observation provenance.</summary>
    [Key(0)] public MarketAnalyticsSignalMetadata Metadata { get; init; } = new();
    /// <summary>Gets the source close.</summary>
    [Key(1)] public decimal Price { get; init; }
    /// <summary>Gets the current EMA10.</summary>
    [Key(2)] public decimal? Ema10 { get; init; }
    /// <summary>Gets the prior EMA10.</summary>
    [Key(3)] public decimal? PreviousEma10 { get; init; }
    /// <summary>Gets current minus prior EMA10.</summary>
    [Key(4)] public decimal? Ema10Slope { get; init; }
    /// <summary>Gets the current EMA20.</summary>
    [Key(5)] public decimal? Ema20 { get; init; }
    /// <summary>Gets the prior EMA20.</summary>
    [Key(6)] public decimal? PreviousEma20 { get; init; }
    /// <summary>Gets current minus prior EMA20.</summary>
    [Key(7)] public decimal? Ema20Slope { get; init; }
    /// <summary>Gets the current EMA50.</summary>
    [Key(8)] public decimal? Ema50 { get; init; }
    /// <summary>Gets the prior EMA50.</summary>
    [Key(9)] public decimal? PreviousEma50 { get; init; }
    /// <summary>Gets current minus prior EMA50.</summary>
    [Key(10)] public decimal? Ema50Slope { get; init; }
    /// <summary>Gets the current EMA200.</summary>
    [Key(11)] public decimal? Ema200 { get; init; }
    /// <summary>Gets the prior EMA200.</summary>
    [Key(12)] public decimal? PreviousEma200 { get; init; }
    /// <summary>Gets current minus prior EMA200.</summary>
    [Key(13)] public decimal? Ema200Slope { get; init; }
    /// <summary>Gets whether EMA200 and its prior value are available.</summary>
    [Key(14)] public bool IsWarm { get; init; }
}

/// <summary>Represents immutable event-sourced state for the complete EMA family.</summary>
[MessagePackObject]
public sealed record FuturesEmaAccumulatorCheckpoint
{
    /// <summary>Gets the number of accepted closes.</summary>
    [Key(0)] public int Count { get; init; }
    /// <summary>Gets the seed sum for EMA10.</summary>
    [Key(1)] public decimal Seed10 { get; init; }
    /// <summary>Gets the seed sum for EMA20.</summary>
    [Key(2)] public decimal Seed20 { get; init; }
    /// <summary>Gets the seed sum for EMA50.</summary>
    [Key(3)] public decimal Seed50 { get; init; }
    /// <summary>Gets the seed sum for EMA200.</summary>
    [Key(4)] public decimal Seed200 { get; init; }
    /// <summary>Gets the current EMA10.</summary>
    [Key(5)] public decimal? Ema10 { get; init; }
    /// <summary>Gets the current EMA20.</summary>
    [Key(6)] public decimal? Ema20 { get; init; }
    /// <summary>Gets the current EMA50.</summary>
    [Key(7)] public decimal? Ema50 { get; init; }
    /// <summary>Gets the current EMA200.</summary>
    [Key(8)] public decimal? Ema200 { get; init; }
    /// <summary>Gets the last observation identity.</summary>
    [Key(9)] public FuturesTradeSessionBarPublisher.FuturesTradeSessionBarId LastObservationId { get; init; }
    /// <summary>Gets the last source sequence.</summary>
    [Key(10)] public long LastSourceSequence { get; init; }
}
