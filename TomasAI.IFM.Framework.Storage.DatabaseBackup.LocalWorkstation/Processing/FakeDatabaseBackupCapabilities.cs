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
        return ValueTask.FromResult(new PostgreSqlBackupBoundary($"fake-boundary-{request.OperationId.Value:N}"));
    }

    public ValueTask<PostgreSqlVerificationResult> VerifyAsync(
        PostgreSqlVerificationRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new PostgreSqlVerificationResult(DatabaseVerificationLevel.Native, true));
    }

    public ValueTask<PostgreSqlRestoreResult> RestoreToFreshTargetAsync(
        PostgreSqlRestoreRequest request,
        IProgress<DatabaseNativeProgress> progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        progress.Report(new DatabaseNativeProgress(DatabaseRecoveryPhase.Validating, 90));
        return ValueTask.FromResult(new PostgreSqlRestoreResult(true, 1));
    }
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
