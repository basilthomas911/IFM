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
using TomasAI.IFM.Shared.SystemAdmin;
using TomasAI.IFM.Shared.SystemAdmin.ServiceApi;
using TomasAI.IFM.Shared.SystemAdmin.Events;
using TomasAI.IFM.Shared.Log.ServiceApi;

namespace TomasAI.IFM.Service.SystemAdmin.Backup
{
    public class DomainDatabaseBackupService : BaseDatabaseBackupService
    {
        /// <summary>
        /// domain database backup service
        /// </summary>
        /// <param name="eventDb"></param>
        /// <param name="statusConsole"></param>
        /// <<param name="systemAdminEventProducer"></param>
        /// <param name="azureStorage"></param>
        public DomainDatabaseBackupService(
            IEventDbContext eventDb,
            IStatusConsoleWriter statusConsole,
            ISystemAdminEventProducer systemAdminEventProducer,
            IAzureStorage azureStorage) :base(statusConsole, systemAdminEventProducer, azureStorage)
        {
            DatabaseBackupMap.Add(DatabaseBackupNames.EventDb, async e => await BackupEventDbAsync(eventDb, e));
        }

        /// <summary>
        /// backup event database
        /// </summary>
        /// <param name="eventDb"></param>
        /// <param name="backupType"></param>
        /// <param name="commandTimeout"></param>
        /// <returns></returns>
        private async Task BackupEventDbAsync(IEventDbContext eventDb, DatabaseBackupEvent e)
            => await BackupDatabaseAsync(e, () => eventDb.BackupDatabaseAsync(e.BackupType, e.CommandTimeout,
                onInfoMessage: async infoMessage => await WriteInfoMessageAsync(infoMessage, e.DatabaseName, e.CommandId)));

    }
}
