using System;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.FuturesContract.Model;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Model;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.UnitTests.MarketData
{
    [TestClass]
    public class BollingerBandsTests
    {
        private static MarketDataDbContext _dbMktData;

        //Use ClassInitialize to run code before running the first test in the class
        [ClassInitialize()]
        public static void MyClassInitialize(TestContext testContext)
        {
            var connSet = new DbConnectionSettings()
                  .Add("MarketDataDbConnection", "Data Source=DEV-SERVER;Initial Catalog=marketdatadb;Integrated Security=True;MultipleActiveResultSets=True", "System.Data.SqlClient");
            var diContainer = new Dictionary<Type, IObjectRepository>();
            var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
            var dbFactory = new DbContextFactory(dbResolver);
            diContainer.Add(typeof(IObjectRepository<MarketDataDbContext>), new MarketDataDbContext(connSet, dbFactory));
            _dbMktData = dbFactory.MarketDataDb as MarketDataDbContext;
        }

        //
        // Use ClassCleanup to run code after all tests in a class have run
        [ClassCleanup()]
        public static void MyClassCleanup()
        {
        }

        [TestMethod]
        public async Task GenerateBollingerBandsOk()
        {
            var normCurvTbl = await _dbMktData.GetNormalCurveTableAsync();
            var eodDataToday = (await _dbMktData.GetFuturesEodDataAsync("ES", DateTime.Now));
            var eodData = new List<FuturesEodDataViewModel> { eodDataToday };
            // get futures eod data from previous 2 months...
            var eodDataRange = await _dbMktData.GetFuturesEodDataByDateRangeAsync("ES", DateTime.Now.AddMonths(-2), DateTime.Now.AddDays(-1));
            eodData.AddRange(eodDataRange);
           
            var bb = new BollingerBands(20, eodData.OrderByDescending(e => e.ValueDate).Take(40).ToArray(), normCurvTbl, new List<VixFuturesEodDataReadModel>());
            var futuresEodData = new FuturesEodData(
                contractId: "ES",
                valueDate: DateTime.Now,
                openPrice: bb.Open,
                highPrice: bb.High,
                lowPrice: bb.Low,
                closePrice: bb.Close,
                volume: bb.Volume,
                dailyPercentChange: bb.DailyPercentageChange,
                dailyStdDev: bb.StdDev,
                dailyStdDevAmount: bb.StdDevAmount,
                upperBand: bb.UpperBand,
                mean: bb.Mean,
                lowerBand: bb.LowerBand,
                marketTrend: bb.MarketTrend,
                marketVolatility: bb.MarketVolatility,
                marketDirection: bb.MarketDirection,
                vixVolatility: bb.VixVolatility,
                fiftyDayMA: bb.FiftyDayMA,
                fiveDayXMA: bb.FiveDayXMA,
                upTrendLimit: bb.UpTrendLimit,
                upTrendingLimit: bb.UpTrendingLimit,
                upTrendMaxLimit: bb.UpTrendMaxLimit,
                downTrendLimit: bb.DownTrendLimit,
                downTrendingLimit: bb.DownTrendingLimit,
                downTrendMaxLimit: bb.DownTrendMaxLimit,
                windowSize: bb.WindowSize
            );

        }
    }
}
