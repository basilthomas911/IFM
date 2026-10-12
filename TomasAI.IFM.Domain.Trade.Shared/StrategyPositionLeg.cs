using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.Trade.Shared;

/// <summary>Current price and basis for one stable position leg.</summary>
[MessagePackObject]
public sealed record StrategyPositionLeg
{
    [Key(0)] public Guid TradeLegId { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S1133", Justification = "Retain historical serialized model members for existing event-log deserialization.")]
    [Key(1), Obsolete("Use ContractId. Broker instrument IDs belong in the broker adapter.")]
    public uint LegacyMarketInstrumentId { get; init; }
    [Key(2)] public int SignedQuantity { get; init; }
    [Key(3)] public decimal OpeningPrice { get; init; }
    [Key(4)] public decimal CurrentPrice { get; init; }
    [Key(5)] public long LastSourceSequence { get; init; }
    [Key(6)] public DateTime LastPriceAtUtc { get; init; }
    [Key(7)] public string ContractId { get; init; } = string.Empty;
    [Key(8)] public TradeAssetFamily AssetFamily { get; init; }
    [Key(9)] public string ContractKey { get; init; } = string.Empty;
    [Key(10)] public DateOnly? Expiry { get; init; }
    [Key(11)] public decimal? Strike { get; init; }
    [Key(12)] public byte? PutCall { get; init; }
}
