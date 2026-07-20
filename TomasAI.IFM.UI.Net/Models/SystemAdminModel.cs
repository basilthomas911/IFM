using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.SystemAdmin;
using TomasAI.IFM.Shared.SystemAdmin.Events;
using TomasAI.IFM.Shared.SystemAdmin.ServiceApi;
using TomasAI.IFM.UI.EventConsumer;

namespace TomasAI.IFM.Models
{
    public class SystemAdminModel : BaseModel<SystemAdminModel>
    {
        readonly ISystemAdminQueryApi _sysAdminQueryApi;
        readonly ISystemAdminCommandApi _sysAdminCommandApi;
        readonly ISystemAdminUIEventConsumer _systemAdminEventConsumer;
        readonly IApplicationUIEventConsumer _applicationEventConsumer;

        /// <summary>
        /// create sysmtem admin model
        /// </summary>
        /// <param name="sysAdminQueryApi"></param>
        /// <param name="sysAdminCommandApi"></param>
        /// <param name="systemAdminEventConsumer"></param>
        /// <param name="applicationEventConsumer"></param>
        public SystemAdminModel(ISystemAdminQueryApi sysAdminQueryApi, 
            ISystemAdminCommandApi sysAdminCommandApi, 
            ISystemAdminUIEventConsumer systemAdminEventConsumer, 
            IApplicationUIEventConsumer applicationEventConsumer)
        {
            _sysAdminQueryApi = sysAdminQueryApi ?? throw new ArgumentNullException(nameof(sysAdminQueryApi));
            _sysAdminCommandApi = sysAdminCommandApi ?? throw new ArgumentNullException(nameof(sysAdminCommandApi));
            _systemAdminEventConsumer = systemAdminEventConsumer ?? throw new ArgumentNullException(nameof(systemAdminEventConsumer));
            _applicationEventConsumer = applicationEventConsumer ?? throw new ArgumentNullException(nameof(applicationEventConsumer));
        }

        /// <summary>
        /// load backup database names
        /// </summary>
        /// <param name="onCompleted"></param>
        public async Task LoadDatabaseNamesAsync(Action<string[]> onCompleted)
            => await ExecuteAsync(() => _sysAdminQueryApi.GetDatabaseNamesAsync(), vm => onCompleted(vm.Names));

        /// <summary>
        /// backup selected database
        /// </summary>
        /// <param name="databaseName"></param>
        /// <param name="backupType"></param>
        /// <param name="commandTimeout"></param>
        public async Task BackupDatabaseAsync(string databaseName, DatabaseBackupType backupType, int commandTimeout)
             => await ExecuteAsync(() => _sysAdminCommandApi.BackupDatabaseAsync(databaseName, backupType, commandTimeout), _ => { });

        /// <summary>
        /// start system admin ui event consumer
        /// </summary>
        /// <param name="backupAction"></param>
        /// <param name="infoMsgAction"></param>
        /// <param name="completedAction"></param>
        /// <param name="failedAction"></param>
        /// <returns></returns>
        public async Task StartSystemAdminEventConsumer(
             Action<DatabaseBackupEvent>? backupAction = null,
             Action<DatabaseBackupInfoMessageEvent>? infoMsgAction = null,
             Action<DatabaseBackupCompleteEvent>? completedAction = null,
            Action<DatabaseBackupFailEvent>? failedAction = null)
        {
            await ExecuteAsync(() =>
            {
                _systemAdminEventConsumer.StartAsync(backupAction!, infoMsgAction!, completedAction!, failedAction!);
                return Task.FromResult( new ServiceResult<Guid>(Guid.NewGuid()));  
            }, _ => { });   
        }
        
        public async Task StopSystemAdminEventConsumer()
        {
            await ExecuteAsync(() =>
            {
                _systemAdminEventConsumer.StopAsync();
                return Task.FromResult(new ServiceResult<Guid>(Guid.NewGuid()));
            }, _ => { });

        }

    }
}
