using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Application.DatabaseBackup.Contracts;

public sealed record DatabaseNativeProgress(DatabaseRecoveryPhase Phase, int Percent);

public sealed record PostgreSqlBackupRequest(
    DatabaseRecoveryOperationId OperationId,
    DatabaseProtectionSetId ProtectionSetId,
    DatabaseBackupLineage? BackupLineage = null);

public sealed record PostgreSqlWalContinuityEvidence(
    string Timeline,
    string StartLsn,
    string EndLsn,
    int RequiredSegmentCount,
    bool RequiredWalPresent);

public sealed record PostgreSqlBackupBoundary(string SafeBoundaryReference)
{
    public PostgreSqlWalContinuityEvidence? WalContinuity { get; init; }
    public DatabaseRecoveryRunStatistics? Statistics { get; init; }
    public int NativeMajorVersion { get; init; }
    public DatabaseBackupLineage? BackupLineage { get; init; }
}

public sealed record PostgreSqlVerificationRequest(
    DatabaseRecoveryOperationId OperationId,
    string SafeBoundaryReference,
    DatabaseBackupLineage? BackupLineage = null);

public sealed record PostgreSqlVerificationResult(DatabaseVerificationLevel Level, bool Succeeded)
{
    public string SafeEvidenceReference { get; init; } = string.Empty;
    public DatabaseRecoveryRunStatistics? Statistics { get; init; }
}

public sealed record PostgreSqlRestoreRequest(
    DatabaseRecoveryOperationId OperationId,
    DatabaseRestorePointId RestorePointId,
    DatabaseFreshTargetDescriptor FreshTarget,
    DatabaseRestorePointId[]? DependencyChain = null,
    PostgreSqlPreparedRecovery? Recovery = null);

public sealed record PostgreSqlPreparedRecovery(
    DateTimeOffset TargetUtc,
    string Timeline,
    string WalArchivePath,
    string[] RequiredSegments);

public sealed record PostgreSqlRestoreResult(bool Succeeded, long ValidationRevision)
{
    public string SafeTargetReference { get; init; } = string.Empty;
    public string SourceSystemIdentifier { get; init; } = string.Empty;
    public string RestoredSystemIdentifier { get; init; } = string.Empty;
    public DatabaseRecoveryRunStatistics? Statistics { get; init; }
}

public interface IDatabaseNativeCapabilityValidation
{
    ValueTask ValidateAsync(CancellationToken cancellationToken);
}

public sealed record DatabaseNativeArtifactDescriptor(string RelativePath, long Length);

/// <summary>
/// Destination-neutral, read-only access to an already verified native backup artifact set.
/// Implementations must reject traversal, links, and files outside their approved native root.
/// </summary>
public interface IDatabaseNativeArtifactSource
{
    ValueTask<IReadOnlyList<DatabaseNativeArtifactDescriptor>> DescribeAsync(
        DatabaseEngine engine,
        DatabaseRecoveryOperationId operationId,
        CancellationToken cancellationToken);

    ValueTask<Stream> OpenReadAsync(
        DatabaseEngine engine,
        DatabaseRecoveryOperationId operationId,
        string relativePath,
        CancellationToken cancellationToken);
}

public interface IDatabaseNativeRestoreArtifactSink
{
    ValueTask PrepareFreshAsync(
        DatabaseEngine engine,
        DatabaseRestorePointId restorePointId,
        CancellationToken cancellationToken);

    ValueTask WriteAsync(
        DatabaseEngine engine,
        DatabaseRestorePointId restorePointId,
        string relativePath,
        Stream source,
        long expectedLength,
        string expectedSha256,
        CancellationToken cancellationToken);
}

public interface IPostgreSqlBackupCapability
{
    ValueTask<PostgreSqlBackupBoundary> CreateBaseBackupAsync(
        PostgreSqlBackupRequest request,
        IProgress<DatabaseNativeProgress> progress,
        CancellationToken cancellationToken);
    ValueTask<PostgreSqlVerificationResult> VerifyAsync(
        PostgreSqlVerificationRequest request,
        CancellationToken cancellationToken);
    ValueTask<PostgreSqlRestoreResult> RestoreToFreshTargetAsync(
        PostgreSqlRestoreRequest request,
        IProgress<DatabaseNativeProgress> progress,
        CancellationToken cancellationToken);
}

public sealed record ScyllaBackupRequest(
    DatabaseRecoveryOperationId OperationId,
    DatabaseProtectionSetId ProtectionSetId,
    DatabaseBackupLineage? BackupLineage = null);

public sealed record ScyllaTopologyEvidence(
    string ClusterName,
    int LiveNodeCount,
    int TokenRangeCount,
    bool SchemaAgreement);

public sealed record ScyllaSnapshotEvidence(
    string SnapshotTag,
    string ManagerTaskReference,
    string SchemaSha256,
    string NativeManifestSha256,
    int KeyspaceCount,
    int TableCount,
    int ArtifactCount,
    string ScyllaVersion,
    string ManagerVersion);

public sealed record ScyllaBackupBoundary(string SafeBoundaryReference)
{
    public ScyllaTopologyEvidence? Topology { get; init; }
    public ScyllaSnapshotEvidence? Snapshot { get; init; }
    public DatabaseRecoveryRunStatistics? Statistics { get; init; }
    public DatabaseBackupLineage? BackupLineage { get; init; }
}

public sealed record ScyllaVerificationRequest(
    DatabaseRecoveryOperationId OperationId,
    string SafeBoundaryReference,
    DatabaseBackupLineage? BackupLineage = null);

public sealed record ScyllaVerificationResult(DatabaseVerificationLevel Level, bool Succeeded)
{
    public string SafeEvidenceReference { get; init; } = string.Empty;
    public ScyllaTopologyEvidence? Topology { get; init; }
    public DatabaseRecoveryRunStatistics? Statistics { get; init; }
}

public sealed record ScyllaRestoreRequest(
    DatabaseRecoveryOperationId OperationId,
    DatabaseRestorePointId RestorePointId,
    DatabaseFreshTargetDescriptor FreshTarget);

public sealed record ScyllaRestoreResult(bool Succeeded, long ValidationRevision)
{
    public string SafeTargetReference { get; init; } = string.Empty;
    public string SourceClusterName { get; init; } = string.Empty;
    public string RestoredClusterName { get; init; } = string.Empty;
    public ScyllaTopologyEvidence? Topology { get; init; }
    public DatabaseRecoveryRunStatistics? Statistics { get; init; }
}

public interface IScyllaBackupCapability
{
    ValueTask<ScyllaBackupBoundary> CreateBackupAsync(
        ScyllaBackupRequest request,
        IProgress<DatabaseNativeProgress> progress,
        CancellationToken cancellationToken);
    ValueTask<ScyllaVerificationResult> VerifyAsync(
        ScyllaVerificationRequest request,
        CancellationToken cancellationToken);
    ValueTask<ScyllaRestoreResult> RestoreToFreshTargetAsync(
        ScyllaRestoreRequest request,
        IProgress<DatabaseNativeProgress> progress,
        CancellationToken cancellationToken);
}
