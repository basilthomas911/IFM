using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Collections.Concurrent;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

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

public sealed class DatabaseBackupSourceHealthRegistry(TimeProvider timeProvider) : IDatabaseBackupSourceHealthRegistry
{
    readonly ConcurrentDictionary<BackupSource, DatabaseBackupSourceHealth> _states = new();

    public IReadOnlyCollection<DatabaseBackupSourceHealth> Snapshot() => _states.Values.OrderBy(static value => value.Source).ToArray();

    public void Set(BackupSource source, bool enabled, bool ready, string status)
    {
        DatabaseBackupEnumValidation.RequireConcrete(source);
        if (string.IsNullOrWhiteSpace(status) || status.Length > 256 || status.Any(char.IsControl))
            throw new ArgumentException("A bounded safe source-health status is required.", nameof(status));
        _states[source] = new(source, enabled, ready, status, timeProvider.GetUtcNow());
    }
}

public sealed class DatabaseBackupSourcesHealthCheck(IDatabaseBackupSourceHealthRegistry sources) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var enabled = sources.Snapshot().Where(static source => source.Enabled).ToArray();
        var unavailable = enabled.Where(static source => !source.Ready).ToArray();
        if (unavailable.Length == 0)
            return Task.FromResult(HealthCheckResult.Healthy("All enabled DatabaseBackup sources are ready."));
        var data = unavailable.ToDictionary(static source => source.Source.ToString(), static source => (object)source.Status);
        return Task.FromResult(HealthCheckResult.Degraded("One or more DatabaseBackup sources are unavailable.", data: data));
    }
}
