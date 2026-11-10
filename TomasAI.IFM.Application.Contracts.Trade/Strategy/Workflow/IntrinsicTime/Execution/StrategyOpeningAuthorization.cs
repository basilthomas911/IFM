using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Execution;

/// <summary>Compact lineage to the immutable committed approval; never duplicates workflow market payloads.</summary>
[MessagePackObject]
public sealed record StrategyOpeningAuthorization
{
    [Key(0)] public Guid Id { get; init; }
    [Key(1)] public Guid CommandId { get; init; }
    [Key(2)] public long EventId { get; init; }
    [Key(3)] public StrategyWorkflowId WorkflowId { get; init; }
    [Key(4)] public long WorkflowRevision { get; init; }
    [Key(5)] public Guid CorrelationId { get; init; }
    [Key(6)] public DateTime UpdatedAtUtc { get; init; }
    [Key(7)] public string StateHash { get; init; } = string.Empty;
    [Key(8)] public StrategyOpeningExecutionRoute Route { get; init; } = new();
    [Key(9)] public string UnderlyingContractId { get; init; } = string.Empty;
}
