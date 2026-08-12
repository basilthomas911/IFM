using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.Events;

namespace TomasAI.IFM.UI.EventConsumer;

public interface ISystemAdminUIEventConsumer
{
    ValueTask StartAsync(
        Action<DatabaseBackupEvent> backupAction = default!,
        Action<DatabaseBackupInfoMessageEvent> infoMsgAction = default!,
        Action<DatabaseBackupCompleteEvent> completedAction = default!,
        Action<DatabaseBackupFailEvent> failedAction = default!);

    /// <summary>Starts observation of authorized public DatabaseBackup domain events.</summary>
    ValueTask StartDatabaseBackupAsync(
        Func<DatabaseBackupEventContract, ValueTask> eventAction,
        CancellationToken cancellationToken = default);

    ValueTask StopAsync();
}
