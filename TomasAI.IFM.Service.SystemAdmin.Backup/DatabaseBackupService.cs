using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Application.Storage.Postgres.LogDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.FundDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.OptionPricerDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.ReferenceDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.TradeDb;
using TomasAI.IFM.Framework.Storage.Azure;
using TomasAI.IFM.Shared.SystemAdmin.ServiceApi;
using TomasAI.IFM.Shared.Log.ServiceApi;

namespace TomasAI.IFM.Service.SystemAdmin.Backup;

/// <summary>
/// database backup service
/// </summary>
/// <param name="dbTrade"></param>
/// <param name="logger"></param>
public class DatabaseBackupService(
    IEventSourceDbContext eventDb,
    IFundDbContext fundDb,
    ILogDbContext logDb,
    IMarketDataDbContext marketDataDb,
    IOptionPricerDbContext optionPricerDb,
    IReferenceDbContext referenceDb,
    ITradeDbContext tradeDb,
    IStatusConsoleWriter statusConsole,
    ISystemAdminEventProducer systemAdminEventProducer,
    IAzureStorage azureStorage) : BaseDatabaseBackupService(statusConsole, systemAdminEventProducer, azureStorage)
{
}
