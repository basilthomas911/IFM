using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;

/// <summary>Represents EMA-centered Bollinger Bands for 10- and 20-observation windows.</summary>
[MessagePackObject]
public sealed record FuturesBbSignalReadModel
{
    /// <summary>Gets common identity and observation provenance.</summary>
    [Key(0)] public MarketAnalyticsSignalMetadata Metadata { get; init; } = new();
    /// <summary>Gets the source close.</summary>
    [Key(1)] public decimal Price { get; init; }
    /// <summary>Gets the EMA10 centerline.</summary>
    [Key(2)] public decimal? Ema10Center { get; init; }
    /// <summary>Gets the population standard deviation for the 10-observation window.</summary>
    [Key(3)] public decimal? StandardDeviation10 { get; init; }
    /// <summary>Gets the upper BB10 value.</summary>
    [Key(4)] public decimal? Upper10 { get; init; }
    /// <summary>Gets the lower BB10 value.</summary>
    [Key(5)] public decimal? Lower10 { get; init; }
    /// <summary>Gets the BB10 width.</summary>
    [Key(6)] public decimal? Width10 { get; init; }
    /// <summary>Gets close position within BB10.</summary>
    [Key(7)] public decimal? Position10 { get; init; }
    /// <summary>Gets the EMA20 centerline.</summary>
    [Key(8)] public decimal? Ema20Center { get; init; }
    /// <summary>Gets the population standard deviation for the 20-observation window.</summary>
    [Key(9)] public decimal? StandardDeviation20 { get; init; }
    /// <summary>Gets the upper BB20 value.</summary>
    [Key(10)] public decimal? Upper20 { get; init; }
    /// <summary>Gets the lower BB20 value.</summary>
    [Key(11)] public decimal? Lower20 { get; init; }
    /// <summary>Gets the BB20 width.</summary>
    [Key(12)] public decimal? Width20 { get; init; }
    /// <summary>Gets close position within BB20.</summary>
    [Key(13)] public decimal? Position20 { get; init; }
    /// <summary>Gets the mean of the prior 20 completed BB20 widths.</summary>
    [Key(14)] public decimal? Width20Baseline { get; init; }
    /// <summary>Gets current BB20 width divided by its positive baseline.</summary>
    [Key(15)] public decimal? Width20Ratio { get; init; }
    /// <summary>Gets whether BB20 and its prior-width baseline are available.</summary>
    [Key(16)] public bool IsWarm { get; init; }
}

/// <summary>Represents immutable event-sourced state for Bollinger calculation.</summary>
[MessagePackObject]
public sealed record FuturesBbAccumulatorCheckpoint
{
    /// <summary>Gets at most the last 20 closes.</summary>
    [Key(0)] public decimal[] Closes { get; init; } = [];
    /// <summary>Gets at most the prior 20 completed positive BB20 widths.</summary>
    [Key(1)] public decimal[] CompletedWidths20 { get; init; } = [];
    /// <summary>Gets the last observation identity.</summary>
    [Key(2)] public FuturesTradeSessionBarId LastObservationId { get; init; }
    /// <summary>Gets the last source sequence.</summary>
    [Key(3)] public long LastSourceSequence { get; init; }
    /// <summary>Gets the exclusive end of the last accepted observation interval.</summary>
    [Key(4)] public DateTimeOffset LastIntervalEndUtc { get; init; }
    /// <summary>Gets the source stream epoch of the last accepted observation.</summary>
    [Key(5)] public Guid LastStreamEpochId { get; init; }
}
