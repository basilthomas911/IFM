using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Storage.YieldCurveRatesDb;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.UnitTests;

[TestClass]
public class YieldCurveRatesDbTests
{
    [TestMethod]
    public async Task CreateYieldCurveRatesOk()
    {
        var dbConn = new DbConnectionSettings()
            .Add("YieldCurveRatesDbConnection", @"Data Source = https://www.quandl.com/api/v3/datasets/USTREASURY/YIELD.csv?api_key=Vpxxmo8BPMwZP-xH8XZZ&start_date=2018-10-16", "TomasAI.IFM.Storage");
        var diContainer = new Dictionary<Type, YieldCurveRatesDbContext>();
        var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
        var dbFactory = new DbContextFactory(dbResolver);
        var logger = Substitute.For<ILogger<DbProvider>>();
        logger.When(_ => { }).Do(_ => { });
        diContainer.Add(typeof(IObjectRepository<YieldCurveRatesDbContext>), new YieldCurveRatesDbContext(dbConn, dbFactory, logger));
        var db = dbFactory.YieldCurveRatesDb as YieldCurveRatesDbContext; 
        var yieldCurveRates = await db.ReadAsync();
        Assert.IsNotNull(yieldCurveRates);
    }

}
