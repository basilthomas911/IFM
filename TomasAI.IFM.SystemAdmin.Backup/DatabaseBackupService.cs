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
using TomasAI.IFM.Shared.Log.ServiceApi;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Service.SystemAdmin.Backup
{
    public class DatabaseBackupService : BaseDatabaseBackupService
    {
        /// <summary>
        /// database backup service
        /// </summary>
        /// <param name="dbTrade"></param>
        /// <param name="logger"></param>
        public DatabaseBackupService(
            IEventDbContext eventDb,
            IFundDbContext fundDb,
            ILogDbContext logDb,
            IMarketDataDbContext marketDataDb,
            IOptionPricerDbContext optionPricerDb,
            IReferenceDbContext referenceDb,
            ITradeDbContext tradeDb,
            IStatusConsoleWriter statusConsole,
            ISystemAdminEventProducer systemAdminEventProducer,
            IAzureStorage azureStorage) :base(statusConsole, systemAdminEventProducer, azureStorage)
        {
        }

    }
}
