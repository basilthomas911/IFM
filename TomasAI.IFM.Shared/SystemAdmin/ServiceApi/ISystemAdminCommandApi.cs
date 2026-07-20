using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.SystemAdmin.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.SystemAdmin;

namespace TomasAI.IFM.Shared.SystemAdmin.ServiceApi
{
    public interface ISystemAdminCommandApi
    {
        Task<ServiceResult<Guid>> BackupDatabaseAsync(string databaseName, DatabaseBackupType backupType, int commandTimeout);
    }
}
