using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.SystemAdmin;

namespace TomasAI.IFM.Application.Storage.TradePlanDb
{
    public interface ITradePlanDbContext
    {
        ITradePlanDbReadContext DbReader { get; }
        ITradePlanDbWriteContext DbWriter { get; }

        Task BackupDatabaseAsync(DatabaseBackupType backupType, int commandTimeout, Action<string> onInfoMessage);
    }
}
