using FluentValidation;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;

/// <summary>Contains the replayable exact futures-session VWAP accumulator.</summary>
[MessagePackObject]
public sealed record FuturesVwapCheckpoint
{
    /// <summary>Gets the futures session start.</summary>
    [Key(0)] public DateTimeOffset SessionStartUtc { get; init; }
    /// <summary>Gets the futures session end.</summary>
    [Key(1)] public DateTimeOffset SessionEndUtc { get; init; }
    /// <summary>Gets the cumulative price multiplied by individual trade size.</summary>
    [Key(2)] public decimal CumulativePriceVolume { get; init; }
    /// <summary>Gets the cumulative eligible executed volume.</summary>
    [Key(3)] public long CumulativeVolume { get; init; }
    /// <summary>Gets the number of eligible trades included.</summary>
    [Key(4)] public long EligibleTradeCount { get; init; }
    /// <summary>Gets the number of rejected or invalidating trades.</summary>
    [Key(5)] public long RejectedTradeCount { get; init; }
    /// <summary>Gets the latest observed trade price.</summary>
    [Key(6)] public decimal LastPrice { get; init; }
    /// <summary>Gets the last accepted source sequence.</summary>
    [Key(7)] public long LastTradeSourceSequence { get; init; }
    /// <summary>Gets the active stream or replay epoch.</summary>
    [Key(8)] public Guid StreamEpochId { get; init; }
    /// <summary>Gets the last accepted live trade ordinal.</summary>
    [Key(9)] public long LastTradeOrdinal { get; init; }
    /// <summary>Gets whether every eligible session trade is represented.</summary>
    [Key(10)] public bool IsValid { get; init; }
    /// <summary>Gets the current invalid reason.</summary>
    [Key(11)] public FuturesVwapInvalidReason InvalidReason { get; init; }
    /// <summary>Gets whether a private exact recovery is active.</summary>
    [Key(12)] public bool IsRecovering { get; init; }
    /// <summary>Gets the active recovery generation.</summary>
    [Key(13)] public Guid RecoveryGenerationId { get; init; }
    /// <summary>Gets the last accepted recovery batch ordinal.</summary>
    [Key(14)] public long RecoveryBatchOrdinal { get; init; } = -1;
    /// <summary>Gets the last calculation time.</summary>
    [Key(15)] public DateTimeOffset AsOfUtc { get; init; }
}
