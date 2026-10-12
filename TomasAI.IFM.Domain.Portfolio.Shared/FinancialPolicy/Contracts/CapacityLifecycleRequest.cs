using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Versioned financial contract: CapacityLifecycleRequest; numeric keys are append-only.</summary>
[MessagePackObject]
public sealed record CapacityLifecycleRequest
{
    [Key(0)] public Guid ReservationId { get; init; }
    [Key(1)] public long ExpectedReservationVersion { get; init; }
    [Key(2)] public CapacityChangeKind ChangeKind { get; init; }
    [Key(3)] public Guid ExecutionId { get; init; }
    [Key(4)] public long ExecutionRevision { get; init; }
    [Key(5)] public LedgerSourceReference Source { get; init; } = new();
    [Key(6)] public int FilledUnits { get; init; }
    [Key(7)] public int CancelledUnits { get; init; }
    [Key(8)] public int RemainingUnits { get; init; }
    [Key(9)] public string RelatedPostingReference { get; init; } = string.Empty;
    [Key(10)] public string ExpectedRequirementsHash { get; init; } = string.Empty;
    [Key(11)] public int ClosedUnits { get; init; }
}
