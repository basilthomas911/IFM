using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.SystemAdmin.Events;
using TomasAI.IFM.Shared.SystemAdmin.ServiceApi;

namespace TomasAI.IFM.Application.Event
{
    public class SystemAdminEvents : IAsyncEventHandler<DatabaseBackupEvent>
    {
        private readonly IDatabaseBackupServiceApi _backupServiceApi;

        public SystemAdminEvents(IDatabaseBackupServiceApi backupServiceApi)
        {
            _backupServiceApi = backupServiceApi;
        }

        public async Task ExecuteAsync(DatabaseBackupEvent e) => await _backupServiceApi.ExecuteAsync(e);

    }
}
