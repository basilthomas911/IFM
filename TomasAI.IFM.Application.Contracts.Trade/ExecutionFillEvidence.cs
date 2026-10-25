using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.Trade.Shared;

/// <summary>Normalized immutable fill evidence accepted by OrderExecution.</summary>
[MessagePackObject]
public sealed record ExecutionFillEvidence
{
    [Key(0)] public Guid ExecutionFillId { get; init; }
    [Key(1)] public Guid ExecutionAttemptId { get; init; }
    [Key(2)] public Guid ComponentId { get; init; }
    [Key(3)] public Guid TradeLegId { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S1133", Justification = "Retain historical serialized model members for existing event-log deserialization.")]
    [Key(4), Obsolete("Use ContractId. Broker instrument IDs belong in the broker adapter.")]
    public uint LegacyMarketInstrumentId { get; init; }
    [Key(5)] public int SignedQuantity { get; init; }
    [Key(6)] public decimal Price { get; init; }
    [Key(7)] public decimal Commission { get; init; }
    [Key(8)] public DateTime FilledAtUtc { get; init; }
    [Key(9)] public string ExternalExecutionId { get; init; } = string.Empty;
    [Key(10)] public string ContractId { get; init; } = string.Empty;
}
