using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using NSubstitute;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.FundDb;
using TomasAI.IFM.Application.Storage.Postgres.LogDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.OptionPricerDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.ReferenceDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.TradeDb;
using TomasAI.IFM.Service.SystemAdmin.Backup;

using TomasAI.IFM.Shared.Log;
using TomasAI.IFM.Shared.Log.ServiceApi;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Azure;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Framework.SequenceId;

namespace TomasAI.IFM.Service.SystemAdmin.UnitTests;

[TestClass]
public class BackupServiceTests
{
    [TestMethod]
    public async Task BackupDatabaseForEventDbOk()
    {
        var logMsgs = new List<string>();
        var dbConn = new DbConnectionSettings()
            .Add("EventDbConnection", "Data Source=DEV-SERVER;Initial Catalog=eventdb;Integrated Security=True;MultipleActiveResultSets=True", "System.Data.SqlClient")
            .Add("FundDbConnection", "Data Source=DEV-SERVER;Initial Catalog=funddb;Integrated Security=True;MultipleActiveResultSets=True", "System.Data.SqlClient")
            .Add("LogDbConnection", "Data Source=DEV-SERVER;Initial Catalog=logdb;Integrated Security=True;MultipleActiveResultSets=True", "System.Data.SqlClient")
            .Add("MarketDataDbConnection", "Data Source=DEV-SERVER;Initial Catalog=marketdatadb;Integrated Security=True;MultipleActiveResultSets=True", "System.Data.SqlClient")
            .Add("OptionPricerDbConnection", "Data Source=DEV-SERVER;Initial Catalog=optionpricerdb;Integrated Security=True;MultipleActiveResultSets=True", "System.Data.SqlClient")
            .Add("ReferenceDbConnection", "Data Source=DEV-SERVER;Initial Catalog=referencedb;Integrated Security=True;MultipleActiveResultSets=True", "System.Data.SqlClient")
            .Add("TradeDbConnection", "Data Source=DEV-SERVER;Initial Catalog=tradedb;Integrated Security=True;MultipleActiveResultSets=True", "System.Data.SqlClient");
        var dbCache = new DbCache();
        var diContainer = new Dictionary<Type, IObjectRepository>();
        var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
        var dbFactory = new DbContextFactory(dbResolver);
        var tradeDbLogger = Substitute.For<ILogger<DbProvider>>();
        tradeDbLogger.When(_ => { }).Do(_ => { });
        var eventDbLogger = Substitute.For<ILogger<DbProvider>>();
        eventDbLogger.When(_ => { }).Do(_ => { });
        var fundDbLogger = Substitute.For<ILogger<DbProvider>>();
        fundDbLogger.When(_ => { }).Do(_ => { });
        var logDbLogger = Substitute.For<ILogger<DbProvider>>();
        logDbLogger.When(_ => { }).Do(_ => { });
        var marketDataDbLogger = Substitute.For<ILogger<DbProvider>>();
        marketDataDbLogger.When(_ => { }).Do(_ => { });
        var optionPricerDbLogger = Substitute.For<ILogger<DbProvider>>();
        optionPricerDbLogger.When(_ => { }).Do(_ => { });
        var referenceDbLogger = Substitute.For<ILogger<DbProvider>>();
        referenceDbLogger.When(_ => { }).Do(_ => { });
        var blackboardService = Substitute.For<IBlackboardService>();
        blackboardService.When(_ => { }).Do(_ => { });
        var sequenceIdGenerator = Substitute.For<ISequenceIdGenerator>();
        sequenceIdGenerator.When(_ => { }).Do(_ => { });

        diContainer.Add(typeof(IObjectRepository<TradeDbContext>), new TradeDbContext(dbConn, dbFactory, sequenceIdGenerator, tradeDbLogger));
        diContainer.Add(typeof(IObjectRepository<EventSourceDbContext>), new EventSourceDbContext(dbConn, dbFactory, blackboardService, eventDbLogger));
        diContainer.Add(typeof(IObjectRepository<FundDbContext>), new FundDbContext(dbConn, dbFactory, sequenceIdGenerator, fundDbLogger));
        diContainer.Add(typeof(IObjectRepository<LogDbContext>), new LogDbContext(dbConn, dbFactory, sequenceIdGenerator, logDbLogger));
        diContainer.Add(typeof(IObjectRepository<MarketDataDbContext>), new MarketDataDbContext(dbConn, dbFactory, blackboardService, sequenceIdGenerator, marketDataDbLogger));
        diContainer.Add(typeof(IObjectRepository<OptionPricerDbContext>), new OptionPricerDbContext(dbConn, dbFactory, sequenceIdGenerator, optionPricerDbLogger));
        diContainer.Add(typeof(IObjectRepository<ReferenceDbContext>), new ReferenceDbContext(dbConn, dbFactory, referenceDbLogger));
        var dbEvent = dbFactory.EventSourceDb as EventSourceDbContext;
        var dbFund = dbFactory.FundDb as FundDbContext;
        var dbLog = dbFactory.LogDb as LogDbContext;
        var dbMarketData = dbFactory.MarketDataDb as MarketDataDbContext;
        var dbOptionPricer = dbFactory.OptionPricerDb as OptionPricerDbContext;
        var dbReference = dbFactory.ReferenceDb as ReferenceDbContext;
        var dbTrade = dbFactory.TradeDb as TradeDbContext;
        var storageUrls = new StorageUrlSettings()
            .Add(StorageNames.DomainData, "https://ifmdatastorage.blob.core.windows.net/domaindata")
            .Add(StorageNames.QueryData, "https://ifmdatastorage.blob.core.windows.net/querydata");
        var options = new AzureStorageOptions();
        var azStore = new AzureStorage(options);

        var mockStatusConsoleWriter = new Mock<IStatusConsoleWriter>(MockBehavior.Strict);
        mockStatusConsoleWriter
            .Setup(e => e.WriteConsoleAsync(It.IsAny<LogSourceType>(), It.IsAny<string>()))
            .Callback<LogSourceType, string>((lst, message) => logMsgs.Add(message));
        /*
        var backupService = new DatabaseBackupService(dbEvent, dbFund, dbLog, dbMarketData, dbOptionPricer, dbReference, dbTrade,   mockStatusConsoleWriter.Object, azStore);
        var backupEvent = new DatabaseBackupEvent
        {
            DatabaseName = DatabaseBackupNames.TradeDb,
            BackupType = DatabaseBackupType.Full,
            CommandTimeout = 5 * 60
        };
        await backupService.ExecuteAsync(backupEvent);
        */
        Assert.IsTrue(logMsgs.Count > 0);


    }
}
