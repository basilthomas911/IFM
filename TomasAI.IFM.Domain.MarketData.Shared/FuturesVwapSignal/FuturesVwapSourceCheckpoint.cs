using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Shared.FuturesVwapSignal;

public enum FuturesVwapSourceInvalidReason : byte
{
    None = 0,
    DeliveryGap = 1,
    StreamEpochChanged = 2,
    UncorrelatableCorrection = 3,
    InvalidTrade = 4,
    RecoveryIncomplete = 5
}

/// <summary>A cumulative, replaceable VWAP observation from the ordered tick source.</summary>
[MessagePackObject]
public sealed record FuturesVwapSourceCheckpoint
{
    public const string Actor = "FuturesVwapSignal";
    [Key(0)] public Guid StreamEpochId { get; init; }
    [Key(1)] public long LastTradeOrdinal { get; init; }
    [Key(2)] public long LastTradeSourceSequence { get; init; }
    [Key(3)] public DateTimeOffset AsOfUtc { get; init; }
    [Key(4)] public decimal CumulativePriceVolume { get; init; }
    [Key(5)] public long CumulativeVolume { get; init; }
    [Key(6)] public long EligibleTradeCount { get; init; }
    [Key(7)] public long RejectedTradeCount { get; init; }
    [Key(8)] public decimal LastPrice { get; init; }
    [Key(9)] public bool IsValid { get; init; }
    [Key(10)] public bool IsReplayComplete { get; init; }
    [Key(11)] public Guid RecoveryGenerationId { get; init; }
    [Key(12)] public ushort Version { get; init; } = 1;
    [Key(13)] public FuturesVwapSourceInvalidReason InvalidReason { get; init; }
}
