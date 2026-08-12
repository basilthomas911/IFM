using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Configuration;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Processing;

public sealed class LocalWorkstationDatabaseRecoveryProcessor(
    IDatabaseBackupExecutionJournal journal,
    IPostgreSqlBackupCapability postgreSql,
    LocalWorkstationDatabaseBackupOptions options)
    : IDatabaseRecoveryProcessor, IDatabaseRecoveryOperationExecutor
{
    readonly IDatabaseBackupExecutionJournal _journal = journal ?? throw new ArgumentNullException(nameof(journal));
    readonly IPostgreSqlBackupCapability _postgreSql = postgreSql ?? throw new ArgumentNullException(nameof(postgreSql));
    readonly LocalWorkstationDatabaseBackupOptions _options = Validate(options);
    readonly DatabaseBackupHostId _hostId = new(options.HostId);

    public BackupSource Source => BackupSource.LocalWorkstation;

    public async ValueTask<DatabaseExecutionAdmission> AdmitAsync(
        DatabaseExecutionIntent intent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(intent);
        intent.Validate();
        if (intent.Source != Source)
            throw new UnsupportedDatabaseBackupSourceException(intent.Source);
        var result = await _journal.AdmitAsync(intent, cancellationToken).ConfigureAwait(false);
        return new DatabaseExecutionAdmission(
            result.OperationId,
            result.Outcome == JournalAdmissionOutcome.Admitted
                ? DatabaseExecutionAdmissionOutcome.Admitted
                : DatabaseExecutionAdmissionOutcome.ExactDuplicate);
    }

    public async ValueTask ExecuteAsync(
        RecoverableJournalOperation operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var intent = operation.Intent;
        if (intent.Source != Source) throw new UnsupportedDatabaseBackupSourceException(intent.Source);
        var lease = await _journal.TryAcquireLeaseAsync(
            intent.OperationId, _hostId, _options.LeaseDuration, cancellationToken).ConfigureAwait(false);
        if (lease is null) return;

        var lastSequence = operation.LastServiceSequence;
        if (lastSequence < 2)
            await EnqueueAsync(DatabaseBackupServiceEventFactory.Started(intent, _hostId, 2), cancellationToken).ConfigureAwait(false);
        await CheckpointAsync(lease, DatabaseRecoveryPhase.Started, terminal: false, "fake-native-started", cancellationToken).ConfigureAwait(false);

        if (intent.ExecutionEvent.Source.OperationKind == DatabaseRecoveryOperationKind.Backup)
        {
            var progress = new Progress<DatabaseNativeProgress>();
            var boundary = await _postgreSql.CreateBaseBackupAsync(
                new PostgreSqlBackupRequest(intent.OperationId, intent.ExecutionEvent.Source.ProtectionSetId),
                progress,
                cancellationToken).ConfigureAwait(false);
            if (lastSequence < 3)
                await EnqueueAsync(DatabaseBackupServiceEventFactory.Boundary(
                    intent, _hostId, 3, boundary.SafeBoundaryReference), cancellationToken).ConfigureAwait(false);
            await CheckpointAsync(lease, DatabaseRecoveryPhase.Capturing, terminal: false, "fake-boundary-created", cancellationToken).ConfigureAwait(false);

            var verification = await _postgreSql.VerifyAsync(
                new PostgreSqlVerificationRequest(intent.OperationId, boundary.SafeBoundaryReference),
                cancellationToken).ConfigureAwait(false);
            if (!verification.Succeeded)
                throw new InvalidOperationException("The fake PostgreSQL verification unexpectedly failed.");
            if (lastSequence < 4)
                await EnqueueAsync(DatabaseBackupServiceEventFactory.Verified(
                    intent, _hostId, 4, verification.Level), cancellationToken).ConfigureAwait(false);
            await CheckpointAsync(lease, DatabaseRecoveryPhase.Verifying, terminal: false, "fake-native-verified", cancellationToken).ConfigureAwait(false);
        }

        var completionSequence = intent.ExecutionEvent.Source.OperationKind == DatabaseRecoveryOperationKind.Backup ? 5 : 3;
        if (lastSequence < completionSequence)
            await EnqueueAsync(DatabaseBackupServiceEventFactory.Completed(
                intent, _hostId, completionSequence), cancellationToken).ConfigureAwait(false);
        await CheckpointAsync(lease, DatabaseRecoveryPhase.Completed, terminal: true, "fake-operation-completed", cancellationToken).ConfigureAwait(false);
    }

    ValueTask EnqueueAsync(
        Domain.SystemAdmin.Shared.DatabaseBackup.Events.DatabaseBackupServiceEventContract @event,
        CancellationToken cancellationToken)
        => _journal.EnqueueServiceEventAsync(new DatabaseServiceEventEnvelope(@event), cancellationToken);

    ValueTask CheckpointAsync(
        JournalLease lease,
        DatabaseRecoveryPhase phase,
        bool terminal,
        string diagnostic,
        CancellationToken cancellationToken)
        => _journal.RecordCheckpointAsync(new JournalCheckpoint(
            lease.OperationId, lease.HostId, lease.FencingToken, phase, terminal, diagnostic, DateTimeOffset.UtcNow),
            cancellationToken);

    static LocalWorkstationDatabaseBackupOptions Validate(LocalWorkstationDatabaseBackupOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        return options;
    }
}
