using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Exact execution capacity changes recorded with the ledger source for deterministic rebuild.</summary>
[MessagePackObject]
public sealed record AcceptedExecutionCapacityFacts
{
    [Key(0)] public ExecutionCapacityAdjustment Outcome { get; init; } = new();
    [Key(1)] public long SourceEventVersion { get; init; }
    [Key(2)] public AcceptedCapacityUsage[] UsageDeltas { get; init; } = [];
}
