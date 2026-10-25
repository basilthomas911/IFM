using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.Trade.Shared;

/// <summary>Accepted close-fill evidence for one existing strategy position.</summary>
[MessagePackObject]
public sealed record PositionCloseExecution
{
    [Key(0)] public StrategyPositionId PositionId { get; init; }
    [Key(1)] public TradeStrategyKind StrategyKind { get; init; }
    [Key(2)] public Guid ExecutionAttemptId { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(3)] public ExecutionFillEvidence[] Fills { get; init; } = [];
    [Key(4)] public DateTime CompletedAtUtc { get; init; }
}
