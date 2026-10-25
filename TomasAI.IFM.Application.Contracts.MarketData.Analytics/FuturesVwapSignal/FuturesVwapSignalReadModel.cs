using FluentValidation;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;

/// <summary>Represents one exact or explicitly invalid futures-session VWAP projection.</summary>
[MessagePackObject]
public sealed record FuturesVwapSignalReadModel
{
    [Key(0)] public string ContractId { get; init; } = string.Empty;
    [Key(1)] public DateOnly ValueDate { get; init; }
    [Key(2)] public string ConfigurationId { get; init; } = string.Empty;
    [Key(3)] public DateTimeOffset SessionStartUtc { get; init; }
    [Key(4)] public DateTimeOffset SessionEndUtc { get; init; }
    [Key(5)] public DateTimeOffset AsOfUtc { get; init; }
    [Key(6)] public decimal CumulativePriceVolume { get; init; }
    [Key(7)] public long CumulativeVolume { get; init; }
    [Key(8)] public long EligibleTradeCount { get; init; }
    [Key(9)] public long RejectedTradeCount { get; init; }
    [Key(10)] public decimal LastPrice { get; init; }
    [Key(11)] public decimal? Vwap { get; init; }
    [Key(12)] public decimal? PriceMinusVwap { get; init; }
    [Key(13)] public decimal? PriceToVwapPercent { get; init; }
    [Key(14)] public long LastTradeSourceSequence { get; init; }
    [Key(15)] public Guid StreamEpochId { get; init; }
    [Key(16)] public long LastTradeOrdinal { get; init; }
    [Key(17)] public bool IsWarm { get; init; }
    [Key(18)] public bool IsValid { get; init; }
    [Key(19)] public FuturesVwapInvalidReason InvalidReason { get; init; }
    [Key(20)] public bool IsTickExact { get; init; }
    [Key(21)] public FuturesVwapCalculationMethod CalculationMethod { get; init; }
    [Key(22)] public int SchemaVersion { get; init; } = 1;
    [Key(23)] public string CalculationVersion { get; init; } = "futures-vwap-exact-v1";
}
