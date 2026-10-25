using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Versioned financial contract: CapacityLifecycleReceipt; numeric keys are append-only.</summary>
[MessagePackObject]
public sealed record CapacityLifecycleReceipt
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid OperationId { get; init; }
    [Key(2)] public Guid ReservationId { get; init; }
    [Key(3)] public long ReservationVersion { get; init; }
    [Key(4)] public long FinancialRevision { get; init; }
    [Key(5)] public ReservationStatus Status { get; init; }
    [Key(6)] public int FilledUnits { get; init; }
    [Key(7)] public int CancelledUnits { get; init; }
    [Key(8)] public int RemainingUnits { get; init; }
    [Key(9)] public string CurrentRequirementsHash { get; init; } = string.Empty;
    [Key(10)] public DateTime CommittedAtUtc { get; init; }
    [Key(11)] public Guid CompletedEventId { get; init; }
    [Key(12)] public string InputHash { get; init; } = string.Empty;
    [Key(13)] public int ClosedUnits { get; init; }
}
