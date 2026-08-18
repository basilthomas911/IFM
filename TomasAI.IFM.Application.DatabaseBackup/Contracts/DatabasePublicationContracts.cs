using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Application.DatabaseBackup.Contracts;

public sealed record DatabaseArtifactDigest(
    string RelativePath,
    long Length,
    string Sha256);

public sealed record DatabaseBackupManifest
{
    public int SchemaVersion { get; init; } = 2;
    public required string ManifestId { get; init; }
    public required DatabaseRecoveryOperationId OperationId { get; init; }
    public required DatabaseRestorePointId RestorePointId { get; init; }
    public BackupSource Source { get; init; } = BackupSource.LocalWorkstation;
    public required DatabaseEngine Engine { get; init; }
    public required DatabaseProtectionSetId ProtectionSetId { get; init; }
    public required string SafeBoundaryReference { get; init; }
    public required DateTimeOffset CreatedUtc { get; init; }
    public long Revision { get; init; } = 1;
    public DatabaseRestorePointId[] Dependencies { get; init; } = [];
    public DatabaseArtifactDigest[] Artifacts { get; init; } = [];
    public DatabaseArtifactReplicaId[] Replicas { get; init; } = [];
    public DatabaseRecoveryRunStatistics? Statistics { get; init; }
    public DatabaseBackupLineage BackupLineage { get; init; } = new();
}

public sealed record DatabaseManifestSignature(
    string KeyId,
    string Algorithm,
    string Value);

public sealed class DatabaseApprovedStorageRoot
{
    internal DatabaseApprovedStorageRoot(string logicalName, string fullPath)
    {
        LogicalName = logicalName;
        FullPath = fullPath;
    }

    public string LogicalName { get; }
    internal string FullPath { get; }
}

public sealed record DatabaseBackupPublicationRequest(
    DatabaseRecoveryOperationId OperationId,
    DatabaseProtectionSetId ProtectionSetId,
    DatabaseEngine Engine,
    string SafeBoundaryReference,
    DatabaseLogicalDestination[] RequiredDestinations,
    DatabaseRecoveryRunStatistics? Statistics = null,
    DatabaseRestorePointId[]? Dependencies = null,
    DatabaseBackupLineage? BackupLineage = null);

public sealed record DatabaseBackupPublicationPreflightRequest(
    DatabaseProtectionSetId ProtectionSetId,
    DatabaseEngine Engine,
    DatabaseLogicalDestination[] RequiredDestinations);

public sealed record DatabaseBackupPublicationResult(
    DatabaseRestorePointId RestorePointId,
    string ManifestId,
    long ManifestRevision,
    DatabaseArtifactReplicaDescriptor[] Replicas);

public sealed record DatabaseRestoreSourceRequest(
    DatabaseRecoveryOperationId OperationId,
    DatabaseRestorePointId RestorePointId,
    DatabaseEngine Engine,
    DatabaseArtifactReplicaId? PreferredReplicaId = null);

public sealed record DatabasePreparedRestoreSource(
    DatabaseRestorePointId NativeRestorePointId,
    DatabaseArtifactReplicaId ReplicaId,
    string ManifestId,
    long ManifestRevision,
    long VerifiedBytes,
    int VerifiedArtifactCount,
    DatabaseRestorePointId[] DependencyChain);

public sealed record DatabaseBackupPlanningRequest(
    DatabaseRecoveryOperationId OperationId,
    DatabaseProtectionSetId ProtectionSetId,
    DatabaseEngine Engine,
    DatabaseBackupMode RequestedMode,
    DatabaseLogicalDestination[] RequiredDestinations);

public interface IDatabaseBackupChainPlanner
{
    ValueTask<DatabaseBackupLineage> PlanAsync(
        DatabaseBackupPlanningRequest request,
        CancellationToken cancellationToken);
}

public sealed record DatabaseReplicaPublicationRequest(
    DatabaseBackupManifest Manifest,
    DatabaseRecoveryOperationId NativeArtifactOperationId,
    DatabaseArtifactReplicaId ReplicaId);

public sealed record DatabaseReplicaPublicationResult(
    DatabaseArtifactReplicaDescriptor Replica,
    string SafeManifestReference,
    string SafeCommitReference);

public sealed record DatabaseMediaEnrollmentRequest(
    string MediaId,
    DatabaseArtifactReplicaId ReplicaId,
    string EnvironmentId,
    string RotationSlot,
    DateTimeOffset EnrolledUtc);

public sealed record DatabaseMediaIdentity(
    int SchemaVersion,
    string MediaId,
    DatabaseArtifactReplicaId ReplicaId,
    string EnvironmentId,
    string RotationSlot,
    DateTimeOffset EnrolledUtc,
    string SigningKeyId,
    string TrustBundleSha256);

public sealed record DatabaseCatalogEntry(
    int SchemaVersion,
    DatabaseRestorePointId RestorePointId,
    string ManifestId,
    long ManifestRevision,
    DatabaseEngine Engine,
    DatabaseProtectionSetId ProtectionSetId,
    DatabaseArtifactReplicaId ReplicaId,
    string ManifestRelativePath,
    string CommitRelativePath,
    DateTimeOffset PublishedUtc);

public sealed record DatabaseCatalogRestorePoint(
    DatabaseCatalogEntry Entry,
    DatabaseBackupManifest Manifest,
    long VerifiedBytes,
    int VerifiedArtifactCount);

public sealed record DatabaseRetentionEvaluationRequest(
    DatabaseRetentionPlanId PlanId,
    long Revision,
    DateTimeOffset EvaluationBoundaryUtc,
    DatabaseArtifactReplicaId ReplicaId,
    DatabaseRestorePointId[] ProtectedRestorePoints,
    DatabaseRestorePointId[] LegalHolds,
    DatabaseRestorePointId[] ActiveRestorePoints);

public sealed record DatabaseRetentionPlanEntry(
    DatabaseRestorePointId RestorePointId,
    string[] ExactRelativePaths);

public sealed record DatabaseRetentionPlan(
    int SchemaVersion,
    DatabaseRetentionPlanId PlanId,
    long Revision,
    DatabaseArtifactReplicaId ReplicaId,
    DateTimeOffset EvaluationBoundaryUtc,
    DateTimeOffset CreatedUtc,
    DatabaseRetentionPlanEntry[] Entries,
    DatabaseRestorePointId[] DependencyProtectedRestorePoints);

public sealed record DatabaseRetentionExecutionRequest(
    DatabaseRetentionPlanId PlanId,
    long Revision,
    DatabaseArtifactReplicaId ReplicaId,
    string ApprovalReference);

public sealed record DatabaseRetentionExecutionResult(
    DatabaseRetentionPlanId PlanId,
    long Revision,
    int DeletedRestorePointCount,
    int DeletedFileCount,
    long DeletedBytes);

public sealed record DatabaseRestoreDrillEvidence(
    DatabaseRecoveryOperationId OperationId,
    DatabaseRestorePointId RestorePointId,
    DatabaseArtifactReplicaId ReplicaId,
    DatabaseEngine Engine,
    DateTimeOffset StartedUtc,
    DateTimeOffset CompletedUtc,
    TimeSpan AchievedRpo,
    TimeSpan AchievedRto,
    bool NativeValidationSucceeded,
    bool ApplicationValidationSucceeded,
    string SafeTargetReference);

public sealed record DatabaseBreakGlassRecoveryRecord
{
    public int SchemaVersion { get; init; } = 1;
    public required DatabaseRecoveryOperationId RecoveryOperationId { get; init; }
    public required DatabaseRestorePointId RestorePointId { get; init; }
    public required DatabaseArtifactReplicaId ReplicaId { get; init; }
    public required string MediaId { get; init; }
    public required string AuthorizationReference { get; init; }
    public required string OperatorIdentity { get; init; }
    public required string RecoveryHostId { get; init; }
    public required string ManifestId { get; init; }
    public required string[] ArtifactVersions { get; init; }
    public required DateTimeOffset StartedUtc { get; init; }
    public required DateTimeOffset CompletedUtc { get; init; }
    public required TimeSpan AchievedRpo { get; init; }
    public required TimeSpan AchievedRto { get; init; }
    public required bool NativeValidationSucceeded { get; init; }
    public required bool ApplicationValidationSucceeded { get; init; }
    public required string CutoverDecision { get; init; }
}

public interface IDatabaseBackupPublicationCapability
{
    ValueTask ValidateAsync(
        DatabaseBackupPublicationPreflightRequest request,
        CancellationToken cancellationToken);
    ValueTask<DatabaseBackupPublicationResult> PublishAsync(
        DatabaseBackupPublicationRequest request,
        CancellationToken cancellationToken);
}

public interface IDatabaseRestoreSourceCapability
{
    ValueTask<DatabasePreparedRestoreSource> PrepareAsync(
        DatabaseRestoreSourceRequest request,
        CancellationToken cancellationToken);
}

public interface ILocalBackupVault
{
    ValueTask<DatabaseMediaIdentity> EnrollAsync(
        DatabaseMediaEnrollmentRequest request,
        CancellationToken cancellationToken);
    ValueTask<DatabaseReplicaPublicationResult> PublishAsync(
        DatabaseReplicaPublicationRequest request,
        CancellationToken cancellationToken);
}

public interface IOfflineBackupMediaProvider
{
    ValueTask<DatabaseMediaIdentity> EnrollAsync(
        DatabaseMediaEnrollmentRequest request,
        CancellationToken cancellationToken);
    ValueTask<DatabaseMediaIdentity> ValidateAttachedMediaAsync(CancellationToken cancellationToken);
    ValueTask<DatabaseReplicaPublicationResult> PublishAsync(
        DatabaseReplicaPublicationRequest request,
        CancellationToken cancellationToken);
}

public interface IRestoreWorkspace
{
    ValueTask<string> StageAsync(
        DatabaseRecoveryOperationId operationId,
        DatabaseCatalogRestorePoint restorePoint,
        CancellationToken cancellationToken);
}

public interface IDatabaseBackupManifestWriter
{
    ValueTask WriteSignedAsync(
        DatabaseApprovedStorageRoot approvedRoot,
        string relativePath,
        DatabaseBackupManifest manifest,
        CancellationToken cancellationToken);
}

public interface IDatabaseBackupManifestReader
{
    ValueTask<DatabaseBackupManifest> ReadAndVerifyAsync(
        DatabaseApprovedStorageRoot approvedRoot,
        string relativePath,
        CancellationToken cancellationToken);
}

public interface IDatabaseBackupCatalog
{
    ValueTask<DatabaseCatalogRestorePoint> ResolveAsync(
        DatabaseRestorePointId restorePointId,
        DatabaseArtifactReplicaId replicaId,
        CancellationToken cancellationToken);
    ValueTask<IReadOnlyList<DatabaseCatalogRestorePoint>> EnumerateAsync(
        DatabaseArtifactReplicaId replicaId,
        CancellationToken cancellationToken);
}

public interface IArtifactChecksumService
{
    ValueTask<DatabaseArtifactDigest> CalculateAsync(
        DatabaseApprovedStorageRoot approvedRoot,
        string relativePath,
        CancellationToken cancellationToken);
}

public interface IManifestSignatureService
{
    string KeyId { get; }
    DatabaseManifestSignature Sign(ReadOnlySpan<byte> content);
    void Verify(ReadOnlySpan<byte> content, DatabaseManifestSignature signature);
}

public interface ILocalBackupCapacityReader
{
    long GetAvailableBytes(DatabaseApprovedStorageRoot approvedRoot);
    void EnsureCapacity(DatabaseApprovedStorageRoot approvedRoot, long requiredBytes, long reserveBytes);
}

public interface IBackupPathPolicy
{
    DatabaseApprovedStorageRoot GetReplicaRoot(DatabaseArtifactReplicaId replicaId);
    DatabaseApprovedStorageRoot GetRestoreWorkspaceRoot();
    DatabaseApprovedStorageRoot GetNativeBackupRoot(DatabaseEngine engine);
    string Resolve(DatabaseApprovedStorageRoot approvedRoot, string normalizedRelativePath);
    void ValidateTree(DatabaseApprovedStorageRoot approvedRoot);
}

public interface IDatabaseRecoveryRunStatsCollector
{
    DatabaseRecoveryRunStatistics Complete(
        DatabaseRecoveryRunStatistics? nativeStatistics,
        DatabaseEngine engine,
        DatabaseRecoveryPhase phase,
        DateTimeOffset startedUtc,
        DateTimeOffset completedUtc,
        long bytes,
        int artifactCount);
}

public interface IDatabaseRetentionCapability
{
    ValueTask<DatabaseRetentionPlan> CreatePlanAsync(
        DatabaseRetentionEvaluationRequest request,
        CancellationToken cancellationToken);
    ValueTask<DatabaseRetentionExecutionResult> ExecuteAsync(
        DatabaseRetentionExecutionRequest request,
        CancellationToken cancellationToken);
}

public interface IDatabaseRecoveryEvidenceStore
{
    ValueTask<string> WriteDrillEvidenceAsync(
        DatabaseRestoreDrillEvidence evidence,
        CancellationToken cancellationToken);
    ValueTask<string> WriteBreakGlassRecordAsync(
        DatabaseBreakGlassRecoveryRecord record,
        CancellationToken cancellationToken);
    ValueTask<DatabaseBreakGlassRecoveryRecord> ReconcileBreakGlassRecordAsync(
        DatabaseRecoveryOperationId operationId,
        CancellationToken cancellationToken);
}
