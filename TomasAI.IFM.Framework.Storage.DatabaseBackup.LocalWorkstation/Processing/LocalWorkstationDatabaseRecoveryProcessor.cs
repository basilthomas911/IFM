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

        switch (intent.ExecutionEvent.Source.OperationKind)
        {
            case DatabaseRecoveryOperationKind.Backup:
                await ExecuteBackupAsync(intent, lease, lastSequence, cancellationToken).ConfigureAwait(false);
                break;
            case DatabaseRecoveryOperationKind.Restore:
            case DatabaseRecoveryOperationKind.RestoreDrill:
                await ExecuteRestoreAsync(intent, lease, lastSequence, cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new NotSupportedException(
                    $"The LocalWorkstation PostgreSQL processor does not implement '{intent.ExecutionEvent.Source.OperationKind}'.");
        }
    }

    async ValueTask ExecuteBackupAsync(
        DatabaseExecutionIntent intent,
        JournalLease lease,
        long lastSequence,
        CancellationToken cancellationToken)
    {
        var progress = new Progress<DatabaseNativeProgress>();
        var boundary = await WithLeaseHeartbeatAsync(lease, token => _postgreSql.CreateBaseBackupAsync(
            new PostgreSqlBackupRequest(intent.OperationId, intent.ExecutionEvent.Source.ProtectionSetId),
            progress,
            token), cancellationToken).ConfigureAwait(false);
        if (lastSequence < 3)
            await EnqueueAsync(DatabaseBackupServiceEventFactory.Boundary(
                intent, _hostId, 3, boundary.SafeBoundaryReference), cancellationToken).ConfigureAwait(false);
        await CheckpointAsync(lease, DatabaseRecoveryPhase.Capturing, terminal: false, "postgresql-boundary-created", cancellationToken).ConfigureAwait(false);

        var verification = await WithLeaseHeartbeatAsync(lease, token => _postgreSql.VerifyAsync(
            new PostgreSqlVerificationRequest(intent.OperationId, boundary.SafeBoundaryReference),
            token), cancellationToken).ConfigureAwait(false);
        if (!verification.Succeeded)
            throw new InvalidOperationException("PostgreSQL native verification failed.");
        if (lastSequence < 4)
            await EnqueueAsync(DatabaseBackupServiceEventFactory.Verified(
                intent, _hostId, 4, verification.Level), cancellationToken).ConfigureAwait(false);
        await CheckpointAsync(lease, DatabaseRecoveryPhase.Verifying, terminal: false, "postgresql-native-verified", cancellationToken).ConfigureAwait(false);

        var statistics = verification.Statistics ?? boundary.Statistics;
        if (statistics is not null && lastSequence < 5)
            await EnqueueAsync(DatabaseBackupServiceEventFactory.Statistics(
                intent, _hostId, 5, DatabaseRecoveryPhase.Verifying, statistics), cancellationToken).ConfigureAwait(false);
        if (lastSequence < 6)
            await EnqueueAsync(DatabaseBackupServiceEventFactory.Completed(
                intent, _hostId, 6), cancellationToken).ConfigureAwait(false);
        await CheckpointAsync(lease, DatabaseRecoveryPhase.Completed, terminal: true, "postgresql-backup-completed", cancellationToken).ConfigureAwait(false);
    }

    async ValueTask ExecuteRestoreAsync(
        DatabaseExecutionIntent intent,
        JournalLease lease,
        long lastSequence,
        CancellationToken cancellationToken)
    {
        var execution = intent.ExecutionEvent;
        if (execution.RestorePointId is null || execution.FreshTarget is null)
            throw new InvalidOperationException("PostgreSQL restore intent requires a restore point and fresh target.");
        var result = await WithLeaseHeartbeatAsync(lease, token => _postgreSql.RestoreToFreshTargetAsync(
            new PostgreSqlRestoreRequest(intent.OperationId, execution.RestorePointId.Value, execution.FreshTarget),
            new Progress<DatabaseNativeProgress>(),
            token), cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded) throw new InvalidOperationException("PostgreSQL fresh-target validation failed.");
        if (lastSequence < 3)
            await EnqueueAsync(DatabaseBackupServiceEventFactory.RestoreValidated(
                intent, _hostId, 3, result), cancellationToken).ConfigureAwait(false);
        await CheckpointAsync(lease, DatabaseRecoveryPhase.Validating, terminal: false, "postgresql-fresh-target-validated", cancellationToken).ConfigureAwait(false);

        if (result.Statistics is not null && lastSequence < 4)
            await EnqueueAsync(DatabaseBackupServiceEventFactory.Statistics(
                intent, _hostId, 4, DatabaseRecoveryPhase.Validating, result.Statistics), cancellationToken).ConfigureAwait(false);
        if (execution.Source.OperationKind == DatabaseRecoveryOperationKind.Restore)
        {
            if (lastSequence < 5)
                await EnqueueAsync(DatabaseBackupServiceEventFactory.ReadyForCutover(
                    intent, _hostId, 5, result), cancellationToken).ConfigureAwait(false);
            await CheckpointAsync(lease, DatabaseRecoveryPhase.ReadyForCutover, terminal: true, "postgresql-ready-for-cutover", cancellationToken).ConfigureAwait(false);
        }
        else
        {
            if (lastSequence < 5)
                await EnqueueAsync(DatabaseBackupServiceEventFactory.Completed(
                    intent, _hostId, 5), cancellationToken).ConfigureAwait(false);
            await CheckpointAsync(lease, DatabaseRecoveryPhase.Completed, terminal: true, "postgresql-restore-drill-completed", cancellationToken).ConfigureAwait(false);
        }
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

    async ValueTask<T> WithLeaseHeartbeatAsync<T>(
        JournalLease lease,
        Func<CancellationToken, ValueTask<T>> operation,
        CancellationToken cancellationToken)
    {
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var heartbeat = RenewLeaseUntilCancelledAsync(lease, lifetime);
        try
        {
            var operationTask = operation(lifetime.Token).AsTask();
            var first = await Task.WhenAny(operationTask, heartbeat).ConfigureAwait(false);
            if (first == heartbeat) await heartbeat.ConfigureAwait(false);
            return await operationTask.ConfigureAwait(false);
        }
        finally
        {
            lifetime.Cancel();
            if (!heartbeat.IsFaulted)
            {
                try { await heartbeat.ConfigureAwait(false); }
                catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
            }
        }
    }

    async Task RenewLeaseUntilCancelledAsync(JournalLease lease, CancellationTokenSource lifetime)
    {
        var interval = TimeSpan.FromTicks(Math.Max(TimeSpan.FromMilliseconds(100).Ticks, lease.LeaseDuration.Ticks / 3));
        try
        {
            while (true)
            {
                await Task.Delay(interval, lifetime.Token).ConfigureAwait(false);
                await _journal.RenewLeaseAsync(lease, lifetime.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch
        {
            lifetime.Cancel();
            throw;
        }
    }

    static LocalWorkstationDatabaseBackupOptions Validate(LocalWorkstationDatabaseBackupOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        return options;
    }
}
