using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Application.DatabaseBackup.Policies;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Application.DatabaseBackup.Processing;

public sealed class DatabaseRecoveryOperationOrchestrator : IDatabaseRecoveryOperationExecutor
{
    readonly IDatabaseCutoverCapability? _cutover;
    readonly IDatabaseRetentionCapability? _retention;
    readonly string _automaticRetentionApprovalReference;
    readonly BackupSource _source;
    readonly IDatabaseBackupExecutionJournal _journal;
    readonly IPostgreSqlBackupCapability _postgreSql;
    readonly IScyllaBackupCapability _scylla;
    readonly IDatabaseBackupPublicationCapability _publication;
    readonly IDatabaseRestoreSourceCapability _restoreSources;
    readonly IDatabaseRecoveryEvidenceStore _evidence;
    readonly IDatabaseBackupChainPlanner _chainPlanner;
    readonly IDatabaseRecoveryEngineSelector _engineSelector;
    readonly DatabaseBackupHostOptions _options;
    readonly DatabaseBackupHostId _hostId;

    public DatabaseRecoveryOperationOrchestrator(
        BackupSource source,
        IDatabaseBackupExecutionJournal journal,
        IPostgreSqlBackupCapability postgreSql,
        IScyllaBackupCapability scylla,
        IDatabaseRecoveryEngineSelector engineSelector,
        DatabaseBackupHostOptions options,
        IDatabaseBackupPublicationCapability publication,
        IDatabaseRestoreSourceCapability restoreSources,
        IDatabaseRecoveryEvidenceStore evidence,
        IDatabaseBackupChainPlanner chainPlanner,
        IDatabaseRetentionCapability? retention = null,
        string automaticRetentionApprovalReference = "",
        IDatabaseCutoverCapability? cutover = null)
    {
        DatabaseBackupEnumValidation.RequireConcrete(source);
        _source = source;
        _cutover = cutover;
        _retention = retention;
        _automaticRetentionApprovalReference = automaticRetentionApprovalReference;
        _journal = journal ?? throw new ArgumentNullException(nameof(journal));
        _postgreSql = postgreSql ?? throw new ArgumentNullException(nameof(postgreSql));
        _scylla = scylla ?? throw new ArgumentNullException(nameof(scylla));
        _publication = publication ?? throw new ArgumentNullException(nameof(publication));
        _restoreSources = restoreSources ?? throw new ArgumentNullException(nameof(restoreSources));
        _evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
        _chainPlanner = chainPlanner ?? throw new ArgumentNullException(nameof(chainPlanner));
        _engineSelector = engineSelector ?? throw new ArgumentNullException(nameof(engineSelector));
        _options = Validate(options);
        _hostId = new DatabaseBackupHostId(options.HostId);
    }

    public BackupSource Source => _source;

    public bool CanProcess(DatabaseProtectionSetId protectionSetId)
        => _engineSelector.CanSelect(protectionSetId);

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
        if (intent.ExecutionEvent.Source.OperationKind == DatabaseRecoveryOperationKind.Retention)
        {
            await ExecuteRetentionAsync(intent, lease, lastSequence, cancellationToken).ConfigureAwait(false);
            return;
        }
        var engine = _engineSelector.Select(intent.ExecutionEvent.Source.ProtectionSetId);
        if (intent.ExecutionEvent.Source.OperationKind == DatabaseRecoveryOperationKind.Cutover)
        {
            await ExecuteCutoverAsync(intent, lease, lastSequence, engine, cancellationToken).ConfigureAwait(false);
            return;
        }
        if (intent.ExecutionEvent.Source.OperationKind == DatabaseRecoveryOperationKind.Backup)
            await _publication.ValidateAsync(
                new DatabaseBackupPublicationPreflightRequest(
                    intent.ExecutionEvent.Source.ProtectionSetId,
                    engine,
                    intent.ExecutionEvent.RequiredDestinations), cancellationToken).ConfigureAwait(false);
        if (lastSequence < 2)
            await EnqueueAsync(DatabaseBackupServiceEventFactory.Started(intent, _hostId, 2), cancellationToken).ConfigureAwait(false);
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
                    $"The {Source} database processor does not implement '{intent.ExecutionEvent.Source.OperationKind}'.");
        }
    }

    async ValueTask ExecuteCutoverAsync(DatabaseExecutionIntent intent, JournalLease lease,
        long lastSequence, DatabaseEngine engine, CancellationToken token)
    {
        var execution = intent.ExecutionEvent;
        var latest = await _journal.ReadLatestServiceEventAsync(intent.OperationId, token).ConfigureAwait(false);
        if (latest is Domain.SystemAdmin.Shared.DatabaseBackup.Events.Service.DatabaseRestoreServiceCompletedEvent
            && latest.Source.OperationKind == DatabaseRecoveryOperationKind.Cutover
            && latest.Source.SourceRevisionOrSequence == lastSequence)
        {
            await CheckpointAsync(lease, DatabaseRecoveryPhase.Completed, true,
                "database-recovery-application-healthy", token).ConfigureAwait(false);
            return;
        }
        var sequence = lastSequence + 1;
        var completionEnqueued = false;
        try
        {
            var capability = _cutover ?? throw new NotSupportedException(
                "Database replacement requires a configured recovery controller outside the API.");
            if (execution.ValidationRevision <= 0 || execution.RestorePointId is null || execution.FreshTarget is null)
                throw new InvalidDataException("Cutover requires the validated restore identity and revision.");
            await CheckpointAsync(lease, DatabaseRecoveryPhase.CuttingOver, false,
                "database-data-replacement-started", token).ConfigureAwait(false);
            var result = await WithLeaseHeartbeatAsync(lease, ct => capability.RecoverAsync(
                new DatabaseCutoverRequest(intent.OperationId, execution.Source.ProtectionSetId,
                    engine, execution.RestorePointId.Value, execution.FreshTarget,
                    execution.ValidationRevision, lease.FencingToken),
                new Progress<DatabaseNativeProgress>(), ct), token).ConfigureAwait(false);
            if (!result.Succeeded)
                throw new InvalidOperationException("Replacement did not restore database health, application health and admission.");
            await EnqueueAsync(DatabaseBackupServiceEventFactory.Create<Domain.SystemAdmin.Shared.DatabaseBackup.Events.Service.DatabaseRestoreServiceCompletedEvent>(
                intent, _hostId, sequence, DatabaseRecoveryPhase.Completed, DatabaseRecoveryOutcome.Succeeded,
                result.SafeEvidenceReference, validationRevision: execution.ValidationRevision) with
                { CutoverState = DatabaseCutoverState.Completed }, token).ConfigureAwait(false);
            completionEnqueued = true;
            await CheckpointAsync(lease, DatabaseRecoveryPhase.Completed, true,
                "database-recovery-application-healthy", token).ConfigureAwait(false);
        }
        catch
        {
            // A durable completion must not be replaced with a conflicting failure at the same sequence.
            if (completionEnqueued) throw;
            // The external controller owns rollback/reconciliation. Never blindly repeat replacement.
            await EnqueueAsync(DatabaseBackupServiceEventFactory.Create<Domain.SystemAdmin.Shared.DatabaseBackup.Events.Service.DatabaseRestoreServiceFailedEvent>(
                intent, _hostId, sequence, DatabaseRecoveryPhase.Failed, DatabaseRecoveryOutcome.Failed,
                "database-cutover-failed-controller-reconciliation-required"), CancellationToken.None).ConfigureAwait(false);
            await CheckpointAsync(lease, DatabaseRecoveryPhase.Failed, true,
                "database-cutover-failed-controller-reconciliation-required", CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    async ValueTask ExecuteRetentionAsync(DatabaseExecutionIntent intent, JournalLease lease, long lastSequence, CancellationToken token)
    {
        var execution = intent.ExecutionEvent;
        var retention = _retention ?? throw new NotSupportedException("Retention capability is not configured for this source.");
        var planId = execution.RetentionPlanId ?? throw new InvalidDataException("Retention requires a plan ID.");
        var replicas = execution.RequiredDestinations.Select(value => new DatabaseArtifactReplicaId(value.Name)).Distinct().ToArray();
        if (replicas.Length != 1)
            throw new InvalidDataException("Each revision-bound retention operation must target exactly one replica.");
        var isEvaluation = execution.Verb == "RetentionEvaluationRequested";
        if (lastSequence > 1)
        {
            await EnqueueAsync(DatabaseBackupServiceEventFactory.Create<Domain.SystemAdmin.Shared.DatabaseBackup.Events.Service.DatabaseRetentionExecutionFailedEvent>(
                intent, _hostId, lastSequence + 1, DatabaseRecoveryPhase.Failed, DatabaseRecoveryOutcome.Failed,
                "retention-interrupted-reconciliation-required"), token).ConfigureAwait(false);
            await CheckpointAsync(lease, DatabaseRecoveryPhase.Failed, true, "retention-interrupted-reconciliation-required", token).ConfigureAwait(false);
            return;
        }
        await EnqueueAsync(DatabaseBackupServiceEventFactory.Started(intent, _hostId, 2), token).ConfigureAwait(false);
        await CheckpointAsync(lease, DatabaseRecoveryPhase.Started, false, "retention-started", token).ConfigureAwait(false);
        var sequence = 3L;
        var automatic = isEvaluation && execution.Request?.Origin == DatabaseRequestOrigin.ScheduledTask
            && !string.IsNullOrWhiteSpace(_automaticRetentionApprovalReference)
            && execution.Request.AuthorizationReference == _automaticRetentionApprovalReference;
        try
        {
            string diagnostic;
            if (isEvaluation)
            {
                var plan = await WithLeaseHeartbeatAsync(lease, ct => retention.CreatePlanAsync(
                    new DatabaseRetentionEvaluationRequest(planId, execution.RetentionPlanRevision,
                        execution.EvaluationBoundaryUtc, replicas[0], [], [], [],
                        execution.Source.PolicyRevision, execution.Source.ProtectionSetId), ct), token).ConfigureAwait(false);
                diagnostic = $"retention-plan-created-{plan.Entries.Length}-restore-points";
            }
            else if (execution.Verb == "RetentionExecutionRequested")
            {
                var result = await WithLeaseHeartbeatAsync(lease, ct => retention.ExecuteAsync(
                    new DatabaseRetentionExecutionRequest(planId, execution.RetentionPlanRevision,
                        replicas[0], execution.Request?.AuthorizationReference ?? ""), ct), token).ConfigureAwait(false);
                diagnostic = $"retention-deleted-{result.DeletedFileCount}-objects-{result.DeletedBytes}-bytes";
            }
            else throw new InvalidDataException("Unknown retention execution verb.");
            var serviceEvent = isEvaluation
                ? DatabaseBackupServiceEventFactory.Create<Domain.SystemAdmin.Shared.DatabaseBackup.Events.Service.DatabaseRetentionPlanCreatedEvent>(
                    intent, _hostId, sequence, automatic ? DatabaseRecoveryPhase.Validating : DatabaseRecoveryPhase.Completed, automatic ? DatabaseRecoveryOutcome.None : DatabaseRecoveryOutcome.Succeeded, diagnostic)
                : (Domain.SystemAdmin.Shared.DatabaseBackup.Events.DatabaseBackupServiceEventContract)
                    DatabaseBackupServiceEventFactory.Create<Domain.SystemAdmin.Shared.DatabaseBackup.Events.Service.DatabaseRetentionExecutionCompletedEvent>(
                    intent, _hostId, sequence, automatic ? DatabaseRecoveryPhase.Validating : DatabaseRecoveryPhase.Completed, automatic ? DatabaseRecoveryOutcome.None : DatabaseRecoveryOutcome.Succeeded, diagnostic);
            await EnqueueAsync(serviceEvent, token).ConfigureAwait(false);
            sequence++;
            if (automatic)
            {
                var result = await WithLeaseHeartbeatAsync(lease, ct => retention.ExecuteAsync(
                    new DatabaseRetentionExecutionRequest(planId, execution.RetentionPlanRevision,
                        replicas[0], _automaticRetentionApprovalReference), ct), token).ConfigureAwait(false);
                diagnostic = $"automatic-retention-deleted-{result.DeletedFileCount}-objects-{result.DeletedBytes}-bytes";
                await EnqueueAsync(DatabaseBackupServiceEventFactory.Create<Domain.SystemAdmin.Shared.DatabaseBackup.Events.Service.DatabaseRetentionExecutionCompletedEvent>(
                    intent, _hostId, sequence, DatabaseRecoveryPhase.Completed, DatabaseRecoveryOutcome.Succeeded, diagnostic), token).ConfigureAwait(false);
            }
            await CheckpointAsync(lease, DatabaseRecoveryPhase.Completed, true, diagnostic, token).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not DatabaseLeaseLostException)
        {
            await EnqueueAsync(DatabaseBackupServiceEventFactory.Create<Domain.SystemAdmin.Shared.DatabaseBackup.Events.Service.DatabaseRetentionExecutionFailedEvent>(
                intent, _hostId, sequence, DatabaseRecoveryPhase.Failed, DatabaseRecoveryOutcome.Failed,
                $"retention-failed-{exception.GetType().Name}"), token).ConfigureAwait(false);
            await CheckpointAsync(lease, DatabaseRecoveryPhase.Failed, true, $"retention-failed-{exception.GetType().Name}", token).ConfigureAwait(false);
            throw;
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
        var requestedMode = intent.ExecutionEvent.BackupLineage?.RequestedMode ?? DatabaseBackupMode.Full;
        var lineage = await _chainPlanner.PlanAsync(
            new DatabaseBackupPlanningRequest(
                intent.OperationId,
                intent.ExecutionEvent.Source.ProtectionSetId,
                engine,
                requestedMode,
                intent.ExecutionEvent.RequiredDestinations),
            cancellationToken).ConfigureAwait(false);
        string boundaryReference;
        DatabaseRecoveryRunStatistics? boundaryStatistics;
        DatabaseVerificationLevel verificationLevel;
        bool verificationSucceeded;
        DatabaseRecoveryRunStatistics? verificationStatistics;
        PostgreSqlWalContinuityEvidence? walContinuity = null;
        ScyllaTopologyEvidence? scyllaTopology = null;
        ScyllaSnapshotEvidence? scyllaSnapshot = null;
        if (engine == DatabaseEngine.PostgreSql)
        {
            var boundary = await WithLeaseHeartbeatAsync(lease, token => _postgreSql.CreateBaseBackupAsync(
                new PostgreSqlBackupRequest(intent.OperationId, intent.ExecutionEvent.Source.ProtectionSetId, lineage),
                progress, token), cancellationToken).ConfigureAwait(false);
            lineage = boundary.BackupLineage ?? lineage;
            walContinuity = boundary.WalContinuity;
            boundaryReference = boundary.SafeBoundaryReference;
            boundaryStatistics = boundary.Statistics;
            var verification = await WithLeaseHeartbeatAsync(lease, token => _postgreSql.VerifyAsync(
                new PostgreSqlVerificationRequest(intent.OperationId, boundaryReference, lineage), token), cancellationToken)
                .ConfigureAwait(false);
            verificationLevel = verification.Level;
            verificationSucceeded = verification.Succeeded;
            verificationStatistics = verification.Statistics;
        }
        else
        {
            var boundary = await WithLeaseHeartbeatAsync(lease, token => _scylla.CreateBackupAsync(
                new ScyllaBackupRequest(intent.OperationId, intent.ExecutionEvent.Source.ProtectionSetId, lineage),
                progress, token), cancellationToken).ConfigureAwait(false);
            lineage = boundary.BackupLineage ?? lineage;
            scyllaTopology = boundary.Topology;
            scyllaSnapshot = boundary.Snapshot;
            boundaryReference = boundary.SafeBoundaryReference;
            boundaryStatistics = boundary.Statistics;
            var verification = await WithLeaseHeartbeatAsync(lease, token => _scylla.VerifyAsync(
                new ScyllaVerificationRequest(intent.OperationId, boundaryReference, lineage), token), cancellationToken)
                .ConfigureAwait(false);
            verificationLevel = verification.Level;
            verificationSucceeded = verification.Succeeded;
            verificationStatistics = verification.Statistics;
        }
        if (lastSequence < 3)
            await EnqueueAsync(DatabaseBackupServiceEventFactory.Boundary(
                intent, _hostId, 3, boundaryReference, lineage), cancellationToken).ConfigureAwait(false);
        await CheckpointAsync(lease, DatabaseRecoveryPhase.Capturing, terminal: false,
            $"{EngineName(engine)}-boundary-created", cancellationToken).ConfigureAwait(false);
        if (!verificationSucceeded)
            throw new InvalidOperationException($"{EngineDisplayName(engine)} native verification failed.");
        if (lastSequence < 4)
            await EnqueueAsync(DatabaseBackupServiceEventFactory.Verified(
                intent, _hostId, 4, verificationLevel, lineage), cancellationToken).ConfigureAwait(false);
        await CheckpointAsync(lease, DatabaseRecoveryPhase.Verifying, terminal: false,
            $"{EngineName(engine)}-native-verified", cancellationToken).ConfigureAwait(false);

        var statistics = verificationStatistics ?? boundaryStatistics;
        var publication = await WithLeaseHeartbeatAsync(lease, token => _publication.PublishAsync(
            new DatabaseBackupPublicationRequest(
                intent.OperationId,
                intent.ExecutionEvent.Source.ProtectionSetId,
                engine,
                boundaryReference,
                intent.ExecutionEvent.RequiredDestinations,
                statistics,
                lineage.NativeKind == DatabaseNativeBackupKind.PostgreSqlIncremental
                    && lineage.ParentRestorePointId is { } parent
                    ? [parent]
                    : [],
                lineage,
                walContinuity,
                scyllaTopology,
                scyllaSnapshot), token), cancellationToken).ConfigureAwait(false);
        var sequence = 5L;
        foreach (var replica in publication.Replicas)
        {
            if (lastSequence < sequence)
                await EnqueueAsync(DatabaseBackupServiceEventFactory.ReplicaPublished(
                    intent, _hostId, sequence, replica, publication.RestorePointId, publication.ManifestRevision, lineage),
                    cancellationToken).ConfigureAwait(false);
            sequence++;
        }
        await CheckpointAsync(lease, DatabaseRecoveryPhase.Transferring, terminal: false,
            $"{EngineName(engine)}-manifest-{publication.ManifestId}-published", cancellationToken).ConfigureAwait(false);

        if (statistics is not null && lastSequence < sequence)
            await EnqueueAsync(DatabaseBackupServiceEventFactory.Statistics(
                intent, _hostId, sequence, DatabaseRecoveryPhase.Verifying, statistics, lineage), cancellationToken).ConfigureAwait(false);
        sequence++;
        if (lastSequence < sequence)
            await EnqueueAsync(DatabaseBackupServiceEventFactory.Completed(
                intent, _hostId, sequence, lineage), cancellationToken).ConfigureAwait(false);
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
        var restoreStartedUtc = DateTimeOffset.UtcNow;
        var prepared = await WithLeaseHeartbeatAsync(lease, token => _restoreSources.PrepareAsync(
            new DatabaseRestoreSourceRequest(intent.OperationId, execution.RestorePointId.Value, engine,
                execution.FreshTarget.PreferredReplicaId,
                PostgreSqlRecoveryTargetUtc: execution.FreshTarget.RecoveryTargetUtc), token),
            cancellationToken).ConfigureAwait(false);
        bool succeeded;
        long validationRevision;
        string safeTargetReference;
        DatabaseRecoveryRunStatistics? statistics;
        if (engine == DatabaseEngine.PostgreSql)
        {
            var result = await WithLeaseHeartbeatAsync(lease, token => _postgreSql.RestoreToFreshTargetAsync(
                new PostgreSqlRestoreRequest(
                    intent.OperationId,
                    prepared.NativeRestorePointId,
                    execution.FreshTarget,
                    prepared.DependencyChain,
                    prepared.PostgreSqlRecovery),
                new Progress<DatabaseNativeProgress>(), token), cancellationToken).ConfigureAwait(false);
            succeeded = result.Succeeded;
            validationRevision = result.ValidationRevision;
            safeTargetReference = result.SafeTargetReference;
            statistics = result.Statistics;
        }
        else
        {
            var result = await WithLeaseHeartbeatAsync(lease, token => _scylla.RestoreToFreshTargetAsync(
                new ScyllaRestoreRequest(
                    intent.OperationId, prepared.NativeRestorePointId, execution.FreshTarget, prepared.ScyllaRecovery),
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
            var completedUtc = DateTimeOffset.UtcNow;
            var achievedRto = statistics?.AchievedRto ?? completedUtc - restoreStartedUtc;
            var achievedRpo = statistics?.AchievedRpo ?? TimeSpan.Zero;
            _ = await WithLeaseHeartbeatAsync(lease, token => _evidence.WriteDrillEvidenceAsync(
                new DatabaseRestoreDrillEvidence(
                    intent.OperationId,
                    execution.RestorePointId.Value,
                    prepared.ReplicaId,
                    engine,
                    restoreStartedUtc,
                    completedUtc,
                    achievedRpo,
                    achievedRto,
                    NativeValidationSucceeded: true,
                    ApplicationValidationSucceeded: true,
                    safeTargetReference), token), cancellationToken).ConfigureAwait(false);
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

    static DatabaseBackupHostOptions Validate(DatabaseBackupHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        return options;
    }

    static string EngineName(DatabaseEngine engine) => engine == DatabaseEngine.ScyllaDb ? "scylla" : "postgresql";
    static string EngineDisplayName(DatabaseEngine engine) => engine == DatabaseEngine.ScyllaDb ? "Scylla" : "PostgreSQL";
}
