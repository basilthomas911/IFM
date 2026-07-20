using TomasAI.IFM.Application.Storage.ScyllaDb.FundDb;
using TomasAI.IFM.Application.Storage.Postgres.LogDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.OptionPricerDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.ReferenceDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.TradeDb;
using TomasAI.IFM.Framework.Storage.Azure;
using TomasAI.IFM.Shared.SystemAdmin;
using TomasAI.IFM.Shared.SystemAdmin.Events;
using TomasAI.IFM.Shared.Log.ServiceApi;
using TomasAI.IFM.Shared.SystemAdmin.ServiceApi;

namespace TomasAI.IFM.Service.SystemAdmin.Backup;

public class QueryDatabaseBackupService : BaseDatabaseBackupService
{
    /// <summary>
    /// query database backup service
    /// </summary>
    /// <param name="fundDb"></param>
    /// <param name="logDb"></param>
    /// <param name="marketDataDb"></param>
    /// <param name="optionPricerDb"></param>
    /// <param name="referenceDb"></param>
    /// <param name="tradeDb"></param>
    /// <param name="statusConsole"></param>
    /// <param name="systemAdminEventProducer"></param>
    /// <param name="azureStorage"></param>
    public QueryDatabaseBackupService(
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
        DatabaseBackupMap.Add(DatabaseBackupNames.FundDb, async e => await BackupFundDbAsync(fundDb, e));
        DatabaseBackupMap.Add(DatabaseBackupNames.LogDb, async e => await BackupLogDbAsync(logDb, e));
        DatabaseBackupMap.Add(DatabaseBackupNames.MarketDataDb, async e => await BackupMarketDataDbAsync(marketDataDb, e));
        DatabaseBackupMap.Add(DatabaseBackupNames.OptionPricerDb, async e => await BackupOptionPricerDbAsync(optionPricerDb, e));
        DatabaseBackupMap.Add(DatabaseBackupNames.ReferenceDb, async e => await BackupReferenceDbAsync(referenceDb, e));
        DatabaseBackupMap.Add(DatabaseBackupNames.TradeDb, async e => await BackupTradeDbAsync(tradeDb, e));
    }

    /// <summary>
    /// backup fund database
    /// </summary>
    /// <param name="fundDb"></param>
    /// <param name="e"></param>
    /// <returns></returns>
    async Task BackupFundDbAsync(IFundDbContext fundDb, DatabaseBackupEvent e)
        => await BackupDatabaseAsync(e, () => fundDb.BackupDatabaseAsync(e.BackupType, e.CommandTimeout, 
            onInfoMessage: async infoMessage => await WriteInfoMessageAsync(infoMessage, e.DatabaseName, e.CommandId)));

    /// <summary>
    /// backup log database
    /// </summary>
    /// <param name="logDb"></param>
    /// <param name="e"></param>
    /// <returns></returns>
    async Task BackupLogDbAsync(ILogDbContext logDb, DatabaseBackupEvent e)
        => await Task.CompletedTask; // TODO: Implement this method   
    /*
        => await BackupDatabaseAsync(e, () => logDb.BackupDatabaseAsync(e.BackupType, e.CommandTimeout,
            onInfoMessage: async infoMessage => await WriteInfoMessageAsync(infoMessage, e.DatabaseName, e.CommandId)));
    */

    /// <summary>
    /// backup market data database
    /// </summary>
    /// <param name="marketDataDb"></param>
    /// <param name="e"></param>
    /// <returns></returns>
    async Task BackupMarketDataDbAsync(IMarketDataDbContext marketDataDb, DatabaseBackupEvent e)
        => await Task.CompletedTask; // TODO: Implement this method   
    /*
        => await BackupDatabaseAsync(e, () => marketDataDb.BackupDatabaseAsync(e.BackupType, e.CommandTimeout,
            onInfoMessage: async infoMessage => await WriteInfoMessageAsync(infoMessage, e.DatabaseName, e.CommandId)));
    */

    /// <summary>
    /// backup option pricer database
    /// </summary>
    /// <param name="optionPricerDb"></param>
    /// <param name="e"></param>
    /// <returns></returns>
    async Task BackupOptionPricerDbAsync(IOptionPricerDbContext optionPricerDb, DatabaseBackupEvent e)
        => await Task.CompletedTask; // TODO: Implement this method   
    /*
        => await BackupDatabaseAsync(e, () => optionPricerDb.BackupDatabaseAsync(e.BackupType, e.CommandTimeout,
            onInfoMessage: async infoMessage => await WriteInfoMessageAsync(infoMessage, e.DatabaseName, e.CommandId)));
    */

    /// <summary>
    /// backup reference database
    /// </summary>
    /// <param name="referenceDb"></param>
    /// <param name="e"></param>
    /// <returns></returns>
    async Task BackupReferenceDbAsync(IReferenceDbContext referenceDb, DatabaseBackupEvent e)
        => await Task.CompletedTask; // TODO: Implement this method   
    /*
        => await BackupDatabaseAsync(e, () => referenceDb.BackupDatabaseAsync(e.BackupType, e.CommandTimeout,
            onInfoMessage: async infoMessage => await WriteInfoMessageAsync(infoMessage, e.DatabaseName, e.CommandId)));
    */

    /// <summary>
    /// backup trade database
    /// </summary>
    /// <param name="tradeDb"></param>
    /// <param name="e"></param>
    /// <returns></returns>
    async Task BackupTradeDbAsync(ITradeDbContext tradeDb, DatabaseBackupEvent e)
        => await Task.CompletedTask; // TODO: Implement this method   
    /*
        => await BackupDatabaseAsync(e, () => tradeDb.BackupDatabaseAsync(e.BackupType, e.CommandTimeout,
            onInfoMessage: async infoMessage => await WriteInfoMessageAsync(infoMessage, e.DatabaseName, e.CommandId)));
    */
}
