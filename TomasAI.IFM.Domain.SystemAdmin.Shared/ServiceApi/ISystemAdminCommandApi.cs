using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.SystemAdmin.Shared;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.ServiceApi
{
    public interface ISystemAdminCommandApi
    {
        Task<ServiceResult<Guid>> BackupDatabaseAsync(string databaseName, DatabaseBackupType backupType, int commandTimeout);
    }
}
