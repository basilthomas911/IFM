using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Versioned financial contract: LedgerMovementEvidence; numeric keys are append-only.</summary>
[MessagePackObject]
public sealed record LedgerMovementEvidence
{
    [Key(0)] public MovementStatus Status { get; init; }
    [Key(1)] public string SourceReference { get; init; } = string.Empty;
    [Key(2)] public DateTime ObservedAtUtc { get; init; }
    [Key(3)] public DateTime ReceivedAtUtc { get; init; }
    [Key(4)] public DateTime ValidUntilUtc { get; init; }
    [Key(5)] public string ContentHash { get; init; } = string.Empty;
}
