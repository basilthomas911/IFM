using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TomasAI.IFM.Api.DatabaseBackup.Host;

public sealed class DatabaseBackupHostHealthState
{
    int _journalReady;
    int _nativeCapabilitiesReady;
    int _ready;
    int _recoverableOperationCount;

    public bool JournalReady => Volatile.Read(ref _journalReady) != 0;
    public bool NativeCapabilitiesReady => Volatile.Read(ref _nativeCapabilitiesReady) != 0;
    public bool Ready => Volatile.Read(ref _ready) != 0;
    public int RecoverableOperationCount => Volatile.Read(ref _recoverableOperationCount);

    public void MarkJournalReady() => Volatile.Write(ref _journalReady, 1);
    public void MarkNativeCapabilitiesReady() => Volatile.Write(ref _nativeCapabilitiesReady, 1);
    public void MarkReady() => Volatile.Write(ref _ready, 1);
    public void MarkNotReady() => Volatile.Write(ref _ready, 0);
    public void SetRecoverableOperationCount(int count) => Volatile.Write(ref _recoverableOperationCount, count);
}

public sealed class DatabaseBackupLivenessHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
        => Task.FromResult(HealthCheckResult.Healthy("Database Backup Host process is alive."));
}

public sealed class DatabaseBackupReadinessHealthCheck(DatabaseBackupHostHealthState state) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
        => Task.FromResult(state.Ready && state.JournalReady && state.NativeCapabilitiesReady
            ? HealthCheckResult.Healthy("Database Backup Host is admitting durable work.")
            : HealthCheckResult.Unhealthy("Database Backup Host is not ready for admission."));
}
