using TomasAI.IFM.Shared.SystemAdmin.Events;

namespace TomasAI.IFM.UI.EventConsumer;

public interface ISystemAdminUIEventConsumer
{
    ValueTask StartAsync(
        Action<DatabaseBackupEvent> backupAction = default!,
        Action<DatabaseBackupInfoMessageEvent> infoMsgAction = default!,
        Action<DatabaseBackupCompleteEvent> completedAction = default!,
        Action<DatabaseBackupFailEvent> failedAction = default!);
    ValueTask StopAsync();
}


