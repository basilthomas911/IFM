namespace TomasAI.IFM.Shared.EventProjector.ReadModels;

/// <summary>
/// Low-cardinality durable backlog measurements for one projector.
/// </summary>
public sealed record EventProjectorOperationalSnapshotReadModel(
    long PendingCount,
    DateTime? OldestPendingAtUtc,
    long BlockedCount,
    long TerminalFailedCount,
    long ExpiredLeaseCount,
    long OutboxPendingCount,
    DateTime? OldestOutboxPendingAtUtc,
    long OutboxRetryCount);
