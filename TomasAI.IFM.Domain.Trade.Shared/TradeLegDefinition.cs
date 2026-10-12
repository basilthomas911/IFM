using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.Trade.Shared;

/// <summary>One stable proposed or established trade leg.</summary>
[MessagePackObject]
public sealed record TradeLegDefinition
{
    [Key(0)] public Guid TradeLegId { get; init; }
    // Key 1 is retained so previously serialized development payloads remain readable.
    // Broker-specific instrument identifiers are never populated or used by new code.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S1133", Justification = "Retain historical serialized model members for existing event-log deserialization.")]
    [Key(1), Obsolete("Use ContractId. Broker instrument IDs belong in the broker adapter.")]
    public uint LegacyMarketInstrumentId { get; init; }
    [Key(2)] public TradeAssetFamily AssetFamily { get; init; }
    [Key(3)] public int SignedQuantity { get; init; }
    [Key(4)] public decimal? LimitPrice { get; init; }
    [Key(5)] public string ContractKey { get; init; } = string.Empty;
    [Key(6)] public DateOnly? Expiry { get; init; }
    [Key(7)] public decimal? Strike { get; init; }
    [Key(8)] public byte? PutCall { get; init; }
    [Key(9)] public string ContractId { get; init; } = string.Empty;
    [Key(10)] public decimal CashMultiplier { get; init; }
}
