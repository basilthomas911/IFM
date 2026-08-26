using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarPublisher;

/// <summary>
/// Stores only immutable futures session facts; derived indicators live in Analytics signal models.
/// </summary>
[MessagePackObject]
public sealed record FuturesEodObservationReadModel
{
    /// <summary>Gets the exact contract or roll-aware continuation identity.</summary>
    [Key(0)] public MarketSeriesIdentity MarketSeriesIdentity { get; init; }
    /// <summary>Gets the actual contract that supplied the session.</summary>
    [Key(1)] public string ContractId { get; init; } = string.Empty;
    /// <summary>Gets the futures value date.</summary>
    [Key(2)] public DateOnly ValueDate { get; init; }
    /// <summary>Gets the exact UTC session start.</summary>
    [Key(3)] public DateTimeOffset SessionStartUtc { get; init; }
    /// <summary>Gets the exact UTC session end.</summary>
    [Key(4)] public DateTimeOffset SessionEndUtc { get; init; }
    /// <summary>Gets the first accepted price.</summary>
    [Key(5)] public decimal Open { get; init; }
    /// <summary>Gets the highest accepted price.</summary>
    [Key(6)] public decimal High { get; init; }
    /// <summary>Gets the lowest accepted price.</summary>
    [Key(7)] public decimal Low { get; init; }
    /// <summary>Gets the final accepted price.</summary>
    [Key(8)] public decimal Close { get; init; }
    /// <summary>Gets the accepted session volume.</summary>
    [Key(9)] public decimal Volume { get; init; }
    /// <summary>Gets the accepted trade count when exact trades were available.</summary>
    [Key(10)] public long TradeCount { get; init; }
    /// <summary>Gets the sum of accepted price multiplied by volume.</summary>
    [Key(11)] public decimal PriceVolumeSum { get; init; }
    /// <summary>Gets the deterministic Daily observation identity.</summary>
    [Key(12)] public FuturesTradeSessionBarId ObservationId { get; init; }
    /// <summary>Gets the first source sequence.</summary>
    [Key(13)] public long FirstSourceSequence { get; init; }
    /// <summary>Gets the last source sequence.</summary>
    [Key(14)] public long LastSourceSequence { get; init; }
    /// <summary>Gets the first included exchange event time.</summary>
    [Key(15)] public DateTimeOffset FirstMarketEventUtc { get; init; }
    /// <summary>Gets the last included exchange event time.</summary>
    [Key(16)] public DateTimeOffset LastMarketEventUtc { get; init; }
    /// <summary>Gets the raw schema version.</summary>
    [Key(17)] public ushort SchemaVersion { get; init; } = 1;
    /// <summary>Gets whether the session reached its calendar barrier.</summary>
    [Key(18)] public bool IsComplete { get; init; }
    /// <summary>Gets whether the raw session facts are internally valid.</summary>
    [Key(19)] public bool IsValid { get; init; }
}
