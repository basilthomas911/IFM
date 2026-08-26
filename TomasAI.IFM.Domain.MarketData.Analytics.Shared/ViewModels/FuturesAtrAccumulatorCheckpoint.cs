using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Observation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

/// <summary>
/// Captures the complete bounded state required to continue a Wilder ATR calculation after event replay.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record FuturesAtrAccumulatorCheckpoint
{
    /// <summary>Gets the configured Wilder period.</summary>
    [Key(0)] public int PeriodLength { get; init; }

    /// <summary>Gets the close from the previously accepted observation.</summary>
    [Key(1)] public decimal? PreviousClose { get; init; }

    /// <summary>Gets the initial true ranges retained until the first ATR is formed.</summary>
    [Key(2)] public decimal[] SeedTrueRanges { get; init; } = [];

    /// <summary>Gets the latest fully formed Wilder ATR.</summary>
    [Key(3)] public decimal? CurrentAtr { get; init; }

    /// <summary>Gets at most twenty previously completed ATR values used by the volatility baseline.</summary>
    [Key(4)] public decimal[] CompletedAtrValues { get; init; } = [];

    /// <summary>Gets the last accepted closed-observation identity.</summary>
    [Key(5)] public FuturesTradeSessionBarId LastObservationId { get; init; }

    /// <summary>Gets the last accepted source sequence.</summary>
    [Key(6)] public long LastSourceSequence { get; init; }

    /// <summary>Gets the market timestamp of the last accepted observation.</summary>
    [Key(7)] public DateTimeOffset? LastMarketEventUtc { get; init; }

    /// <summary>Gets the number of unique observations incorporated into this stream.</summary>
    [Key(8)] public long ObservationCount { get; init; }

    /// <summary>Creates an empty checkpoint for the supplied period.</summary>
    public static FuturesAtrAccumulatorCheckpoint Empty(int periodLength) => new()
    {
        PeriodLength = periodLength
    };
}
