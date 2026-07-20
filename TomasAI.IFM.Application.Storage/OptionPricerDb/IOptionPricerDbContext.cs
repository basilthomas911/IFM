using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.SystemAdmin;

namespace TomasAI.IFM.Application.Storage.OptionPricerDb
{
    public interface IOptionPricerDbContext
    {
        IOptionPricerDbReadContext DbReader { get; }
        IOptionPricerDbWriteContext DbWriter { get; }

        Task BackupDatabaseAsync(DatabaseBackupType backupType, int commandTimeout, Action<string> onInfoMessage);

    }
}
