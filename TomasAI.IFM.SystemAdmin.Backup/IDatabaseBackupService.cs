using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.SystemAdmin.Events;

namespace TomasAI.IFM.Service.SystemAdmin.Backup
{
    public interface IDatabaseBackupService
    {
        Task ExecuteAsync(DatabaseBackupEvent e);
    }
}
