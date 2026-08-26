using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarPublisher;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

/// <summary>Represents immutable event-sourced Wilder RSI calculation state.</summary>
[MessagePackObject]
public sealed record FuturesRsiAccumulatorCheckpoint
{
    /// <summary>Gets the configured Wilder period.</summary>
    [Key(0)] public int PeriodLength { get; init; }

    /// <summary>Gets the last accepted close.</summary>
    [Key(1)] public decimal? PreviousClose { get; init; }

    /// <summary>Gets accumulated gains while seeding.</summary>
    [Key(2)] public decimal SeedGainSum { get; init; }

    /// <summary>Gets accumulated losses while seeding.</summary>
    [Key(3)] public decimal SeedLossSum { get; init; }

    /// <summary>Gets the current Wilder average gain once seeded.</summary>
    [Key(4)] public decimal? AverageGain { get; init; }

    /// <summary>Gets the current Wilder average loss once seeded.</summary>
    [Key(5)] public decimal? AverageLoss { get; init; }

    /// <summary>Gets the most recently calculated RSI.</summary>
    [Key(6)] public double? CurrentRsi { get; init; }

    /// <summary>Gets the number of accepted price changes.</summary>
    [Key(7)] public int ChangeCount { get; init; }

    /// <summary>Gets the last accepted observation identity.</summary>
    [Key(8)] public FuturesTradeSessionBarId LastObservationId { get; init; }

    /// <summary>Gets the last accepted source sequence.</summary>
    [Key(9)] public long LastSourceSequence { get; init; }

    /// <summary>Gets the last accepted market event time.</summary>
    [Key(10)] public DateTimeOffset LastMarketEventUtc { get; init; }
}
