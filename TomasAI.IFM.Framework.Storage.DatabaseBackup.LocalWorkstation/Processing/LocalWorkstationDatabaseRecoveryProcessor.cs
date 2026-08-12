using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Configuration;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Processing;

public sealed class LocalWorkstationDatabaseRecoveryProcessor
    : IDatabaseRecoveryProcessor, IDatabaseRecoveryOperationExecutor
{
    readonly IDatabaseBackupExecutionJournal _journal;
    readonly IPostgreSqlBackupCapability _postgreSql;
    readonly IScyllaBackupCapability _scylla;
    readonly IDatabaseRecoveryEngineSelector _engineSelector;
    readonly LocalWorkstationDatabaseBackupOptions _options;
    readonly DatabaseBackupHostId _hostId;

    public LocalWorkstationDatabaseRecoveryProcessor(
        IDatabaseBackupExecutionJournal journal,
        IPostgreSqlBackupCapability postgreSql,
        LocalWorkstationDatabaseBackupOptions options)
        : this(journal, postgreSql, new FakeScyllaBackupCapability(),
            new PostgreSqlOnlyDatabaseRecoveryEngineSelector(), options) { }

    public LocalWorkstationDatabaseRecoveryProcessor(
        IDatabaseBackupExecutionJournal journal,
        IPostgreSqlBackupCapability postgreSql,
        IScyllaBackupCapability scylla,
        IDatabaseRecoveryEngineSelector engineSelector,
        LocalWorkstationDatabaseBackupOptions options)
    {
        _journal = journal ?? throw new ArgumentNullException(nameof(journal));
        _postgreSql = postgreSql ?? throw new ArgumentNullException(nameof(postgreSql));
        _scylla = scylla ?? throw new ArgumentNullException(nameof(scylla));
        _engineSelector = engineSelector ?? throw new ArgumentNullException(nameof(engineSelector));
        _options = Validate(options);
        _hostId = new DatabaseBackupHostId(options.HostId);
    }

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
        var engine = _engineSelector.Select(intent.ExecutionEvent.Source.ProtectionSetId);
        await CheckpointAsync(lease, DatabaseRecoveryPhase.Started, terminal: false,
            $"{EngineName(engine)}-native-started", cancellationToken).ConfigureAwait(false);

        switch (intent.ExecutionEvent.Source.OperationKind)
        {
            case DatabaseRecoveryOperationKind.Backup:
                await ExecuteBackupAsync(intent, lease, lastSequence, engine, cancellationToken).ConfigureAwait(false);
                break;
            case DatabaseRecoveryOperationKind.Restore:
            case DatabaseRecoveryOperationKind.RestoreDrill:
                await ExecuteRestoreAsync(intent, lease, lastSequence, engine, cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new NotSupportedException(
                    $"The LocalWorkstation database processor does not implement '{intent.ExecutionEvent.Source.OperationKind}'.");
        }
    }

    async ValueTask ExecuteBackupAsync(
        DatabaseExecutionIntent intent,
        JournalLease lease,
        long lastSequence,
        DatabaseEngine engine,
        CancellationToken cancellationToken)
    {
        var progress = new Progress<DatabaseNativeProgress>();
        string boundaryReference;
        DatabaseRecoveryRunStatistics? boundaryStatistics;
        DatabaseVerificationLevel verificationLevel;
        bool verificationSucceeded;
        DatabaseRecoveryRunStatistics? verificationStatistics;
        if (engine == DatabaseEngine.PostgreSql)
        {
            var boundary = await WithLeaseHeartbeatAsync(lease, token => _postgreSql.CreateBaseBackupAsync(
                new PostgreSqlBackupRequest(intent.OperationId, intent.ExecutionEvent.Source.ProtectionSetId),
                progress, token), cancellationToken).ConfigureAwait(false);
            boundaryReference = boundary.SafeBoundaryReference;
            boundaryStatistics = boundary.Statistics;
            var verification = await WithLeaseHeartbeatAsync(lease, token => _postgreSql.VerifyAsync(
                new PostgreSqlVerificationRequest(intent.OperationId, boundaryReference), token), cancellationToken)
                .ConfigureAwait(false);
            verificationLevel = verification.Level;
            verificationSucceeded = verification.Succeeded;
            verificationStatistics = verification.Statistics;
        }
        else
        {
            var boundary = await WithLeaseHeartbeatAsync(lease, token => _scylla.CreateBackupAsync(
                new ScyllaBackupRequest(intent.OperationId, intent.ExecutionEvent.Source.ProtectionSetId),
                progress, token), cancellationToken).ConfigureAwait(false);
            boundaryReference = boundary.SafeBoundaryReference;
            boundaryStatistics = boundary.Statistics;
            var verification = await WithLeaseHeartbeatAsync(lease, token => _scylla.VerifyAsync(
                new ScyllaVerificationRequest(intent.OperationId, boundaryReference), token), cancellationToken)
                .ConfigureAwait(false);
            verificationLevel = verification.Level;
            verificationSucceeded = verification.Succeeded;
            verificationStatistics = verification.Statistics;
        }
        if (lastSequence < 3)
            await EnqueueAsync(DatabaseBackupServiceEventFactory.Boundary(
                intent, _hostId, 3, boundaryReference), cancellationToken).ConfigureAwait(false);
        await CheckpointAsync(lease, DatabaseRecoveryPhase.Capturing, terminal: false,
            $"{EngineName(engine)}-boundary-created", cancellationToken).ConfigureAwait(false);
        if (!verificationSucceeded)
            throw new InvalidOperationException($"{EngineDisplayName(engine)} native verification failed.");
        if (lastSequence < 4)
            await EnqueueAsync(DatabaseBackupServiceEventFactory.Verified(
                intent, _hostId, 4, verificationLevel), cancellationToken).ConfigureAwait(false);
        await CheckpointAsync(lease, DatabaseRecoveryPhase.Verifying, terminal: false,
            $"{EngineName(engine)}-native-verified", cancellationToken).ConfigureAwait(false);

        var statistics = verificationStatistics ?? boundaryStatistics;
        if (statistics is not null && lastSequence < 5)
            await EnqueueAsync(DatabaseBackupServiceEventFactory.Statistics(
                intent, _hostId, 5, DatabaseRecoveryPhase.Verifying, statistics), cancellationToken).ConfigureAwait(false);
        if (lastSequence < 6)
            await EnqueueAsync(DatabaseBackupServiceEventFactory.Completed(
                intent, _hostId, 6), cancellationToken).ConfigureAwait(false);
        await CheckpointAsync(lease, DatabaseRecoveryPhase.Completed, terminal: true,
            $"{EngineName(engine)}-backup-completed", cancellationToken).ConfigureAwait(false);
    }

    async ValueTask ExecuteRestoreAsync(
        DatabaseExecutionIntent intent,
        JournalLease lease,
        long lastSequence,
        DatabaseEngine engine,
        CancellationToken cancellationToken)
    {
        var execution = intent.ExecutionEvent;
        if (execution.RestorePointId is null || execution.FreshTarget is null)
            throw new InvalidOperationException("Database restore intent requires a restore point and fresh target.");
        bool succeeded;
        long validationRevision;
        string safeTargetReference;
        DatabaseRecoveryRunStatistics? statistics;
        if (engine == DatabaseEngine.PostgreSql)
        {
            var result = await WithLeaseHeartbeatAsync(lease, token => _postgreSql.RestoreToFreshTargetAsync(
                new PostgreSqlRestoreRequest(intent.OperationId, execution.RestorePointId.Value, execution.FreshTarget),
                new Progress<DatabaseNativeProgress>(), token), cancellationToken).ConfigureAwait(false);
            succeeded = result.Succeeded;
            validationRevision = result.ValidationRevision;
            safeTargetReference = result.SafeTargetReference;
            statistics = result.Statistics;
        }
        else
        {
            var result = await WithLeaseHeartbeatAsync(lease, token => _scylla.RestoreToFreshTargetAsync(
                new ScyllaRestoreRequest(intent.OperationId, execution.RestorePointId.Value, execution.FreshTarget),
                new Progress<DatabaseNativeProgress>(), token), cancellationToken).ConfigureAwait(false);
            succeeded = result.Succeeded;
            validationRevision = result.ValidationRevision;
            safeTargetReference = result.SafeTargetReference;
            statistics = result.Statistics;
        }
        if (!succeeded) throw new InvalidOperationException($"{EngineDisplayName(engine)} fresh-target validation failed.");
        if (lastSequence < 3)
            await EnqueueAsync(DatabaseBackupServiceEventFactory.RestoreValidated(
                intent, _hostId, 3, safeTargetReference, validationRevision), cancellationToken).ConfigureAwait(false);
        await CheckpointAsync(lease, DatabaseRecoveryPhase.Validating, terminal: false,
            $"{EngineName(engine)}-fresh-target-validated", cancellationToken).ConfigureAwait(false);

        if (statistics is not null && lastSequence < 4)
            await EnqueueAsync(DatabaseBackupServiceEventFactory.Statistics(
                intent, _hostId, 4, DatabaseRecoveryPhase.Validating, statistics), cancellationToken).ConfigureAwait(false);
        if (execution.Source.OperationKind == DatabaseRecoveryOperationKind.Restore)
        {
            if (lastSequence < 5)
                await EnqueueAsync(DatabaseBackupServiceEventFactory.ReadyForCutover(
                    intent, _hostId, 5, safeTargetReference, validationRevision), cancellationToken).ConfigureAwait(false);
            await CheckpointAsync(lease, DatabaseRecoveryPhase.ReadyForCutover, terminal: true,
                $"{EngineName(engine)}-ready-for-cutover", cancellationToken).ConfigureAwait(false);
        }
        else
        {
            if (lastSequence < 5)
                await EnqueueAsync(DatabaseBackupServiceEventFactory.Completed(
                    intent, _hostId, 5), cancellationToken).ConfigureAwait(false);
            await CheckpointAsync(lease, DatabaseRecoveryPhase.Completed, terminal: true,
                $"{EngineName(engine)}-restore-drill-completed", cancellationToken).ConfigureAwait(false);
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

    static string EngineName(DatabaseEngine engine) => engine == DatabaseEngine.ScyllaDb ? "scylla" : "postgresql";
    static string EngineDisplayName(DatabaseEngine engine) => engine == DatabaseEngine.ScyllaDb ? "Scylla" : "PostgreSQL";
}
