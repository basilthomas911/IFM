using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.Trade.Shared;

/// <summary>State owned by one broker-neutral execution attempt.</summary>
[MessagePackObject]
public sealed record OrderExecutionDefinition
{
    [Key(0)] public ushort SchemaVersion { get; init; } = 6;
    [Key(1)] public TradeOrderId TradeOrderId { get; init; }
    [Key(2)] public Guid ExecutionAttemptId { get; init; }
    [Key(3)] public ExecutionChannel Channel { get; init; }
    [Key(4)] public OrderExecutionStatus Status { get; init; }
    [Key(5)] public int OrderRevision { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(6)] public TradeOrderComponentDefinition[] Components { get; init; } = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(7)] public ExecutionFillEvidence[] Fills { get; init; } = [];
    [Key(8)] public DateTime StartedAtUtc { get; init; }
    [Key(9)] public DateTime? CompletedAtUtc { get; init; }
    [Key(10)] public TradeOrderPositionType PositionType { get; init; }
    [Key(11)] public StrategyPositionId? TargetPositionId { get; init; }
    [Key(12)] public TradeOrderDefinition Order { get; init; } = new();
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(13)] public PendingExecutionCostEvidence[] PendingFillCosts { get; init; } = [];
    [Key(14)] public int OrderQuantity { get; init; }
    [Key(15)] public int CumulativeFilledQuantity { get; init; }
    [IgnoreMember] public OrderExecutionId Id => new(TradeOrderId, ExecutionAttemptId);
}
