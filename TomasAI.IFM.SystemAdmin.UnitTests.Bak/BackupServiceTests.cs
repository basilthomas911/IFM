using System;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Application.Core.MarketData;
using TomasAI.IFM.Application.Storage.EventDb;
using TomasAI.IFM.Application.Storage.EventQueueDb;
using TomasAI.IFM.Application.Storage.FundDb;
using TomasAI.IFM.Application.Storage.LogDb;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.OptionPricerDb;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.SystemAdmin.Backup;

using TomasAI.IFM.Shared.Log;
using TomasAI.IFM.Shared.Log.ServiceApi;
using TomasAI.IFM.Shared.Log.ViewModels;
using TomasAI.IFM.Shared.SignalR;
using TomasAI.IFM.Shared.SystemAdmin;
using TomasAI.IFM.Shared.SystemAdmin.Events;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.SystemAdmin.UnitTests
{
    [TestClass]
    public class BackupServiceTests
    {
        [TestMethod]
        public async Task BackupDatabaseForEventDbOk()
        {
            var logMsgs = new List<string>();
            var dbConn = new DbConnectionSettings()
                .Add("EventDbConnection", "Data Source=DEV-SERVER;Initial Catalog=eventdb;Integrated Security=True;MultipleActiveResultSets=True", "System.Data.SqlClient")
                .Add("EventQueueDbConnection", "Data Source=DEV-SERVER;Initial Catalog=eventqueuedb;Integrated Security=True;MultipleActiveResultSets=True", "System.Data.SqlClient")
                .Add("FundDbConnection", "Data Source=DEV-SERVER;Initial Catalog=funddb;Integrated Security=True;MultipleActiveResultSets=True", "System.Data.SqlClient")
                .Add("LogDbConnection", "Data Source=DEV-SERVER;Initial Catalog=logdb;Integrated Security=True;MultipleActiveResultSets=True", "System.Data.SqlClient");
            var dbEvent = new EventDbContext(dbConn);
            var dbEventQueue = new EventQueueDbContext(dbConn);
            var dbFund = new FundDbContext(dbConn);
            var dbLog = new LogDbContext(dbConn);
            var storageUrls = new StorageUrlSettings()
                .Add(StorageNames.DomainData, "https://ifmdatastorage.blob.core.windows.net/domaindata")
                .Add(StorageNames.QueryData, "https://ifmdatastorage.blob.core.windows.net/querydata");

            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLogReadModel>()))
                .ReturnsAsync((StatusConsoleLogReadModel o) => new ServiceOk<StatusConsoleLogReadModel>(o))
                .Callback<StatusConsoleLogReadModel>(vm => logMsgs.Add(vm.Message));

            var backupEvents = new BackupService(dbEvent, dbEventQueue, dbFund, dbLog, storageUrls, mockStatusConsoleServiceApi.Object);
            var backupEvent = new DatabaseBackupEvent
            {
                DatabaseName = DatabaseBackupNames.LogDb,
                BackupType = DatabaseBackupType.Full,
                CommandTimeout = 5 * 60
            };
            await backupEvents.ExecuteAsync(backupEvent);
            Assert.IsTrue(logMsgs.Count > 0);


        }
    }
}
