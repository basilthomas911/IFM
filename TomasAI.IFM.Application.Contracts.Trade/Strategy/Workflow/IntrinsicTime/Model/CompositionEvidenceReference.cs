using MessagePack;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;

/// <summary>Exact immutable market preparation saved before workflow acceptance; contains no nested serialized payload.</summary>
[MessagePackObject]
public sealed record CompositionEvidenceReference(
    [property: Key(0)] Guid WorkflowId,
    [property: Key(1)] long PreparationRevision,
    [property: Key(2)] string InputSha256,
    [property: Key(3)] Guid SnapshotId,
    [property: Key(4)] string PreparationSha256,
    [property: Key(5)] DateTimeOffset ValidUntilUtc);
