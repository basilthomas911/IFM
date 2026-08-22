using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Application.DatabaseBackup.Contracts;

public class DatabaseBackupHostOptions
{
    public const string SectionName = "DatabaseBackup:Host";

    public string HostId { get; set; } = $"backup-{Environment.MachineName}";
    public int DispatcherCount { get; set; } = 1;
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(2);
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMilliseconds(250);
    public TimeSpan FailedOperationRetryDelay { get; set; } = TimeSpan.FromMinutes(5);
    public int OutboxBatchSize { get; set; } = 64;

    public void Validate()
    {
        _ = new DatabaseBackupHostId(HostId);
        if (DispatcherCount <= 0 || OutboxBatchSize <= 0)
            throw new InvalidOperationException("DatabaseBackup dispatcher and outbox bounds must be positive.");
        if (LeaseDuration <= TimeSpan.Zero || PollInterval <= TimeSpan.Zero || FailedOperationRetryDelay <= TimeSpan.Zero)
            throw new InvalidOperationException("DatabaseBackup lease, poll, and failed-operation retry intervals must be positive.");
    }
}

public sealed record DatabaseBackupSourceHealth(
    BackupSource Source,
    bool Enabled,
    bool Ready,
    string Status,
    DateTimeOffset ObservedUtc);

public interface IDatabaseBackupSourceHealthRegistry
{
    IReadOnlyCollection<DatabaseBackupSourceHealth> Snapshot();
    void Set(BackupSource source, bool enabled, bool ready, string status);
}
