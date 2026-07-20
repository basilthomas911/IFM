using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Application.Storage.FundDb;

namespace TomasAI.IFM.Application.Storage.UnitTests.SqlServer
{
    public class FundDbTests
    {
        /*
        [Fact]
        public async Task GetFundPnlOk()
        {
            var dbConn = new DbConnectionSettings()
                .Add("FundDbConnection", @"Data Source=DEV-SERVER;Initial Catalog=fundtestdb;Integrated Security=True;MultipleActiveResultSets=True", "System.Data.SqlClient");
            var db = new FundDbContext(dbConn);
            var startDate = new DateTime(2019, 1, 1);
            var endDate = new DateTime(2019, 12, 31);
            var fundPnl = await db.GetFundPnlAsync(1003, startDate, endDate);
            Assert.NotNull(fundPnl);
            Assert.True(fundPnl.Count > 0);
        }
        */
    }
}
