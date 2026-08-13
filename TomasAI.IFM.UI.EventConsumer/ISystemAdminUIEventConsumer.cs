using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;

namespace TomasAI.IFM.UI.EventConsumer;

public interface ISystemAdminUIEventConsumer
{
    /// <summary>Starts observation of authorized public DatabaseBackup domain events.</summary>
    ValueTask StartDatabaseBackupAsync(
        Func<DatabaseBackupEventContract, ValueTask> eventAction,
        CancellationToken cancellationToken = default);

    ValueTask StopAsync();
}
