using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Application.DatabaseBackup.Contracts;

public sealed record DatabaseNativeProgress(DatabaseRecoveryPhase Phase, int Percent);

public sealed record PostgreSqlBackupRequest(
    DatabaseRecoveryOperationId OperationId,
    DatabaseProtectionSetId ProtectionSetId);

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
}

public sealed record PostgreSqlVerificationRequest(
    DatabaseRecoveryOperationId OperationId,
    string SafeBoundaryReference);

public sealed record PostgreSqlVerificationResult(DatabaseVerificationLevel Level, bool Succeeded)
{
    public string SafeEvidenceReference { get; init; } = string.Empty;
    public DatabaseRecoveryRunStatistics? Statistics { get; init; }
}

public sealed record PostgreSqlRestoreRequest(
    DatabaseRecoveryOperationId OperationId,
    DatabaseRestorePointId RestorePointId,
    DatabaseFreshTargetDescriptor FreshTarget);

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
    DatabaseProtectionSetId ProtectionSetId);

public sealed record ScyllaBackupBoundary(string SafeBoundaryReference);

public sealed record ScyllaVerificationRequest(
    DatabaseRecoveryOperationId OperationId,
    string SafeBoundaryReference);

public sealed record ScyllaVerificationResult(DatabaseVerificationLevel Level, bool Succeeded);

public sealed record ScyllaRestoreRequest(
    DatabaseRecoveryOperationId OperationId,
    DatabaseRestorePointId RestorePointId,
    DatabaseFreshTargetDescriptor FreshTarget);

public sealed record ScyllaRestoreResult(bool Succeeded, long ValidationRevision);

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
