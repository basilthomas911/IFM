using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using TomasAI.IFM.Application.Storage.EventDb;
using TomasAI.IFM.Application.Storage.FundDb;
using TomasAI.IFM.Application.Storage.LogDb;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.OptionPricerDb;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Framework.Storage.Azure;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.SystemAdmin;
using TomasAI.IFM.Shared.SystemAdmin.Events;
using TomasAI.IFM.Shared.SystemAdmin.ServiceApi;
using TomasAI.IFM.Shared.Log;
using TomasAI.IFM.Shared.Log.ServiceApi;
using TomasAI.IFM.Shared.Log.Events;
using TomasAI.IFM.Shared.Log.ViewModels;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Service.SystemAdmin.Backup
{
    public abstract class BaseDatabaseBackupService : IDatabaseBackupService
    {
        readonly IStatusConsoleWriter _statusConsole;
        readonly ISystemAdminEventProducer _systemAdminEventProducer;
        readonly IAzureStorage _azureStorage;
        readonly Dictionary<string, Func<DatabaseBackupEvent, Task>> _dbBackupMap;

        /// <summary>
        /// created event handlers for spreead trade generated events
        /// </summary>
        /// <param name="systemAdminEventProducer"></param>
        public BaseDatabaseBackupService(IStatusConsoleWriter statusConsole, ISystemAdminEventProducer systemAdminEventProducer, IAzureStorage azureStorage)
        {
            _statusConsole = statusConsole; 
            _systemAdminEventProducer = systemAdminEventProducer;
            _azureStorage = azureStorage;
            _dbBackupMap = new Dictionary<string, Func<DatabaseBackupEvent, Task>>();
        }

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
                    await _systemAdminEventProducer.PostEventAsync(e.ToCompletedEvent());
                }
            }
            catch (Exception ex)
            {
                await _systemAdminEventProducer.PostEventAsync( e.ToFailedEvent(ex));    
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
}
