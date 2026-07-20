using TomasAI.IFM.Framework.Storage.Azure;
using TomasAI.IFM.Shared.SystemAdmin;
using TomasAI.IFM.Shared.SystemAdmin.Events;
using TomasAI.IFM.Shared.SystemAdmin.ServiceApi;
using TomasAI.IFM.Shared.Log;
using TomasAI.IFM.Shared.Log.ServiceApi;

namespace TomasAI.IFM.Service.SystemAdmin.Backup;

/// <summary>
/// created event handlers for spreead trade generated events
/// </summary>
/// <param name="systemAdminEventProducer"></param>
public abstract class BaseDatabaseBackupService(IStatusConsoleWriter statusConsole, ISystemAdminEventProducer systemAdminEventProducer, IAzureStorage azureStorage) : IDatabaseBackupService
{
    readonly IStatusConsoleWriter _statusConsole = statusConsole;
    readonly ISystemAdminEventProducer _systemAdminEventProducer = systemAdminEventProducer;
    readonly IAzureStorage _azureStorage = azureStorage;
    readonly Dictionary<string, Func<DatabaseBackupEvent, Task>> _dbBackupMap = [];

    protected Dictionary<string, Func<DatabaseBackupEvent, Task>> DatabaseBackupMap => _dbBackupMap;

    protected async Task BackupDatabaseAsync(DatabaseBackupEvent e, Func<Task> backupDatabaseAction)
    {
        var backupType = e.BackupType == DatabaseBackupType.Full ? "full" : "differential";
        var storageFile = _azureStorage.GetStorageFile(e.DatabaseName, e.BackupType);
        await WriteInfoMessageAsync($"Starting {e.BackupType} backup of {e.DatabaseName}...", e.DatabaseName, e.CommandId);
        await _statusConsole.WriteConsoleAsync(LogSourceType.DatabaseBackup, $"Starting {backupType} backup of {e.DatabaseName}...");
        await backupDatabaseAction();
        await _statusConsole.WriteConsoleAsync(LogSourceType.DatabaseBackup, $"Uploading {storageFile.Source} to {storageFile.Container}\\{storageFile.Destination}...");
        await _azureStorage.UploadFileAsync($"{e.DatabaseName}", $"{e.BackupType}".ToLower(), async (infoMessage) => await WriteInfoMessageAsync(infoMessage, e.DatabaseName, e.CommandId));
        await _statusConsole.WriteConsoleAsync(LogSourceType.DatabaseBackup, $"Completed {backupType} backup of {e.DatabaseName}");
    }

    /// <summary>
    /// backup selected database
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task ExecuteAsync(DatabaseBackupEvent e)
    {
        try
        {
            if (_dbBackupMap.ContainsKey(e.DatabaseName))
            {
                var dbBackup = _dbBackupMap[e.DatabaseName];
                await dbBackup(e);
                await Task.Delay(TimeSpan.FromSeconds(2));
                //await _systemAdminEventProducer.PostEventAsync(e.ToCompletedEvent());
            }
        }
        catch (Exception ex)
        {
            //await _systemAdminEventProducer.PostEventAsync( e.ToFailedEvent(ex));    
        }
    }

    /// <summary>
    /// write info message
    /// </summary>
    /// <param name="infoMessage"></param>
    /// <param name="backupTag"></param>
    /// <returns></returns>
    protected async Task WriteInfoMessageAsync(string infoMessage, string backupTag, Guid commandId)
        => await _systemAdminEventProducer.PostEventAsync(new DatabaseBackupInfoMessageEvent {
            CommandId = commandId,  
            DatabaseName = backupTag,
            InfoMessage = infoMessage,
            CreatedOn = DateTime.Now
        });

}
