using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Processing;

public sealed class FakePostgreSqlBackupCapability : IPostgreSqlBackupCapability
{
    public ValueTask<PostgreSqlBackupBoundary> CreateBaseBackupAsync(
        PostgreSqlBackupRequest request,
        IProgress<DatabaseNativeProgress> progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        progress.Report(new DatabaseNativeProgress(DatabaseRecoveryPhase.Capturing, 50));
        return ValueTask.FromResult(new PostgreSqlBackupBoundary($"fake-boundary-{request.OperationId.Value:N}")
        {
            NativeMajorVersion = 15,
            WalContinuity = new PostgreSqlWalContinuityEvidence("1", "0/1000000", "0/2000000", 1, true),
            Statistics = Statistics(DatabaseRecoveryPhase.Capturing, sourceBytes: 1024)
        });
    }

    public ValueTask<PostgreSqlVerificationResult> VerifyAsync(
        PostgreSqlVerificationRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new PostgreSqlVerificationResult(DatabaseVerificationLevel.Native, true)
        {
            SafeEvidenceReference = "fake-native-verification",
            Statistics = Statistics(DatabaseRecoveryPhase.Verifying, sourceBytes: 1024)
        });
    }

    public ValueTask<PostgreSqlRestoreResult> RestoreToFreshTargetAsync(
        PostgreSqlRestoreRequest request,
        IProgress<DatabaseNativeProgress> progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        progress.Report(new DatabaseNativeProgress(DatabaseRecoveryPhase.Validating, 90));
        return ValueTask.FromResult(new PostgreSqlRestoreResult(true, 1)
        {
            SafeTargetReference = $"fake-target-{request.OperationId.Value:N}",
            SourceSystemIdentifier = "1",
            RestoredSystemIdentifier = "1",
            Statistics = Statistics(DatabaseRecoveryPhase.Validating, restoredBytes: 1024)
        });
    }

    static DatabaseRecoveryRunStatistics Statistics(
        DatabaseRecoveryPhase phase,
        long? sourceBytes = null,
        long? restoredBytes = null)
        => new()
        {
            Engine = DatabaseEngine.PostgreSql,
            Phase = phase,
            StartedUtc = DateTimeOffset.UtcNow,
            CompletedUtc = DateTimeOffset.UtcNow,
            Elapsed = TimeSpan.Zero,
            SourceBytes = sourceBytes,
            StoredBytes = sourceBytes,
            RestoredBytes = restoredBytes,
            ArtifactCount = 1,
            RetryCount = 0,
            WarningCount = 0
        };
}

public sealed class FakeScyllaBackupCapability : IScyllaBackupCapability
{
    public ValueTask<ScyllaBackupBoundary> CreateBackupAsync(
        ScyllaBackupRequest request,
        IProgress<DatabaseNativeProgress> progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        progress.Report(new DatabaseNativeProgress(DatabaseRecoveryPhase.Capturing, 50));
        return ValueTask.FromResult(new ScyllaBackupBoundary($"fake-boundary-{request.OperationId.Value:N}")
        {
            Topology = new ScyllaTopologyEvidence("fake-scylla", 1, 1, true),
            Snapshot = new ScyllaSnapshotEvidence(
                "sm_20000101000000UTC", "backup/fake", new string('a', 64), new string('b', 64),
                1, 1, 1, "fake", "fake"),
            Statistics = Statistics(DatabaseRecoveryPhase.Capturing, sourceBytes: 1024)
        });
    }

    public ValueTask<ScyllaVerificationResult> VerifyAsync(
        ScyllaVerificationRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new ScyllaVerificationResult(DatabaseVerificationLevel.Native, true)
        {
            SafeEvidenceReference = "fake-scylla-native-verification",
            Topology = new ScyllaTopologyEvidence("fake-scylla", 1, 1, true),
            Statistics = Statistics(DatabaseRecoveryPhase.Verifying, sourceBytes: 1024)
        });
    }

    public ValueTask<ScyllaRestoreResult> RestoreToFreshTargetAsync(
        ScyllaRestoreRequest request,
        IProgress<DatabaseNativeProgress> progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        progress.Report(new DatabaseNativeProgress(DatabaseRecoveryPhase.Validating, 90));
        return ValueTask.FromResult(new ScyllaRestoreResult(true, 1)
        {
            SafeTargetReference = $"fake-scylla-target-{request.OperationId.Value:N}",
            SourceClusterName = "fake-scylla",
            RestoredClusterName = "fake-scylla-fresh",
            Topology = new ScyllaTopologyEvidence("fake-scylla-fresh", 1, 1, true),
            Statistics = Statistics(DatabaseRecoveryPhase.Validating, restoredBytes: 1024)
        });
    }

    static DatabaseRecoveryRunStatistics Statistics(
        DatabaseRecoveryPhase phase,
        long? sourceBytes = null,
        long? restoredBytes = null)
        => new()
        {
            Engine = DatabaseEngine.ScyllaDb,
            Phase = phase,
            StartedUtc = DateTimeOffset.UtcNow,
            CompletedUtc = DateTimeOffset.UtcNow,
            Elapsed = TimeSpan.Zero,
            SourceBytes = sourceBytes,
            StoredBytes = sourceBytes,
            RestoredBytes = restoredBytes,
            ArtifactCount = 1,
            RetryCount = 0,
            WarningCount = 0
        };
}

public sealed class FakeDatabaseBackupPublicationCapability : IDatabaseBackupPublicationCapability
{
    public ValueTask ValidateAsync(
        DatabaseBackupPublicationPreflightRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }

    public ValueTask<DatabaseBackupPublicationResult> PublishAsync(
        DatabaseBackupPublicationRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var restorePoint = new DatabaseRestorePointId(request.OperationId.Format());
        var replicaId = request.RequiredDestinations.Length == 0
            ? new DatabaseArtifactReplicaId("fake-online-vault")
            : new DatabaseArtifactReplicaId(request.RequiredDestinations[0].Name);
        return ValueTask.FromResult(new DatabaseBackupPublicationResult(
            restorePoint,
            $"fake-manifest-{request.OperationId.Value:N}",
            1,
            [new DatabaseArtifactReplicaDescriptor
            {
                ArtifactId = new DatabaseArtifactId($"fake-artifact-{request.OperationId.Value:N}"),
                ReplicaId = replicaId,
                Engine = request.Engine,
                State = DatabaseArtifactReplicaState.Published,
                SafeDestinationReference = $"{replicaId.Value}:{restorePoint.Value}",
                Bytes = request.Statistics?.StoredBytes
            }]));
    }
}

public sealed class FakeDatabaseRestoreSourceCapability : IDatabaseRestoreSourceCapability
{
    public ValueTask<DatabasePreparedRestoreSource> PrepareAsync(
        DatabaseRestoreSourceRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new DatabasePreparedRestoreSource(
            request.RestorePointId,
            request.PreferredReplicaId ?? new DatabaseArtifactReplicaId("fake-online-vault"),
            $"fake-manifest-{request.RestorePointId.Value}",
            1,
            1024,
            1));
    }
}

public sealed class FakeDatabaseRecoveryEvidenceStore : IDatabaseRecoveryEvidenceStore
{
    public ValueTask<string> WriteDrillEvidenceAsync(
        DatabaseRestoreDrillEvidence evidence,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult($"fake-drill-{evidence.OperationId.Value:N}");
    }

    public ValueTask<string> WriteBreakGlassRecordAsync(
        DatabaseBreakGlassRecoveryRecord record,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult($"fake-recovery-{record.RecoveryOperationId.Value:N}");
    }

    public ValueTask<DatabaseBreakGlassRecoveryRecord> ReconcileBreakGlassRecordAsync(
        DatabaseRecoveryOperationId operationId,
        CancellationToken cancellationToken)
        => throw new FileNotFoundException("The fake evidence store has no persisted break-glass records.");
}
