using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;

namespace TomasAI.IFM.Application.DatabaseBackup.Contracts;

public enum JournalAdmissionOutcome
{
    Admitted = 1,
    ExactDuplicate = 2
}

public sealed record JournalAdmissionResult(
    DatabaseRecoveryOperationId OperationId,
    JournalAdmissionOutcome Outcome);

public sealed record JournalLease(
    DatabaseRecoveryOperationId OperationId,
    DatabaseBackupHostId HostId,
    long FencingToken,
    DateTimeOffset ExpiresUtc,
    TimeSpan LeaseDuration);

public sealed record JournalCheckpoint(
    DatabaseRecoveryOperationId OperationId,
    DatabaseBackupHostId HostId,
    long FencingToken,
    DatabaseRecoveryPhase Phase,
    bool Terminal,
    string SafeDiagnosticReference,
    DateTimeOffset ObservedUtc);

public sealed record DatabaseServiceEventEnvelope(DatabaseBackupServiceEventContract Event);

public sealed record PendingServiceEvent(
    Guid EventId,
    DatabaseRecoveryOperationId OperationId,
    long ServiceSequence,
    DatabaseBackupServiceEventContract Event,
    int PublishAttempts);

public sealed record RecoverableJournalOperation(
    DatabaseExecutionIntent Intent,
    DatabaseRecoveryPhase Phase,
    long LastServiceSequence,
    long FencingToken);

public interface IDatabaseBackupExecutionJournal
{
    ValueTask InitializeAsync(CancellationToken cancellationToken);
    ValueTask VerifyIntegrityAsync(CancellationToken cancellationToken);
    ValueTask<JournalAdmissionResult> AdmitAsync(DatabaseExecutionIntent intent, CancellationToken cancellationToken);
    ValueTask<JournalLease?> TryAcquireLeaseAsync(
        DatabaseRecoveryOperationId operationId,
        DatabaseBackupHostId hostId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken);
    ValueTask RenewLeaseAsync(JournalLease lease, CancellationToken cancellationToken);
    ValueTask RecordCheckpointAsync(JournalCheckpoint checkpoint, CancellationToken cancellationToken);
    ValueTask EnqueueServiceEventAsync(DatabaseServiceEventEnvelope envelope, CancellationToken cancellationToken);
    IAsyncEnumerable<PendingServiceEvent> ReadPendingServiceEventsAsync(
        int maximumCount,
        CancellationToken cancellationToken);
    ValueTask MarkServiceEventPublishedAsync(
        Guid eventId,
        DateTimeOffset publishedUtc,
        CancellationToken cancellationToken);
    IAsyncEnumerable<RecoverableJournalOperation> ReadRecoverableOperationsAsync(CancellationToken cancellationToken);
    ValueTask MarkCoreAcknowledgedAsync(
        DatabaseRecoveryOperationId operationId,
        long domainRevision,
        CancellationToken cancellationToken);
}

public interface IDatabaseRecoveryOperationExecutor
{
    ValueTask ExecuteAsync(RecoverableJournalOperation operation, CancellationToken cancellationToken);
}
