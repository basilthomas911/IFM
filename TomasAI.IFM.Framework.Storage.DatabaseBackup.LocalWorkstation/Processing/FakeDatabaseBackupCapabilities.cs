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
        return ValueTask.FromResult(new ScyllaBackupBoundary($"fake-boundary-{request.OperationId.Value:N}"));
    }

    public ValueTask<ScyllaVerificationResult> VerifyAsync(
        ScyllaVerificationRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new ScyllaVerificationResult(DatabaseVerificationLevel.Native, true));
    }

    public ValueTask<ScyllaRestoreResult> RestoreToFreshTargetAsync(
        ScyllaRestoreRequest request,
        IProgress<DatabaseNativeProgress> progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        progress.Report(new DatabaseNativeProgress(DatabaseRecoveryPhase.Validating, 90));
        return ValueTask.FromResult(new ScyllaRestoreResult(true, 1));
    }
}
