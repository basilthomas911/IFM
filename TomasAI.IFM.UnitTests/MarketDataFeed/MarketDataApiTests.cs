using System;
using System.Threading;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Linq;
using System.Threading.Tasks.Dataflow;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using TomasAI.IFM.Service.MarketDataFeed.InteractiveBrokers;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Storage.OptionPricerDb;

namespace TomasAI.IFM.UnitTest.UnitTests
{
    [TestClass]
    public class MarketDataApiTests
    {
        private static IBMarketDataApi _mdApi;
        private static MarketDataDbContext _dbMktData;
        private static OptionPricerDbContext _dbOptPricer;
        private static TradeDbContext _dbTrade;
        private static object _threadlock;

        //Use ClassInitialize to run code before running the first test in the class
        [ClassInitialize()]
        public static void MyClassInitialize(TestContext testContext)
        {
             var connSet = new DbConnectionSettings()
                  .Add("MarketDataDbConnection", "Data Source=DEV-SERVER;Initial Catalog=marketdatadb;Integrated Security=True;MultipleActiveResultSets=True", "System.Data.SqlClient")
                  .Add("OptionPricerDbConnection", "Data Source=DEV-SERVER;Initial Catalog=optionpricerdb;Integrated Security=True;MultipleActiveResultSets=True", "System.Data.SqlClient")
                  .Add("TradeDbConnection", "Data Source=DEV-SERVER;Initial Catalog=tradedb;Integrated Security=True;MultipleActiveResultSets=True", "System.Data.SqlClient");

            var diContainer = new Dictionary<Type, IObjectRepository>();
            var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
            var dbFactory = new DbContextFactory(dbResolver);
            diContainer.Add(typeof(IObjectRepository<TradeDbContext>), new TradeDbContext(connSet, dbFactory));
            diContainer.Add(typeof(IObjectRepository<OptionPricerDbContext>), new OptionPricerDbContext(connSet, dbFactory));
            diContainer.Add(typeof(IObjectRepository<MarketDataDbContext>), new MarketDataDbContext(connSet, dbFactory));
            var db = dbFactory.TradeDb as TradeDbContext;

            _dbMktData = dbFactory.MarketDataDb as MarketDataDbContext;
            _dbOptPricer = dbFactory.OptionPricerDb as OptionPricerDbContext;
            _dbTrade = dbFactory.TradeDb as TradeDbContext;
            _threadlock = new object();
            var options = new IBMarketDataApiOptions("127.0.0.1", 7496, 1);
            _mdApi = new IBMarketDataApi(options, null, null);
        }

        //
        // Use ClassCleanup to run code after all tests in a class have run
        [ClassCleanup()]
        public static void MyClassCleanup()
        {
            _mdApi.Stop();
        }

        [TestMethod]
        public async Task GetFuturesContractOk()
        {
            var qfContract = (await _dbMktData.GetFuturesContractAsync("ES20180615"));
            var contract = await _mdApi.GetFuturesContractAsync(RequestID.FuturesContract, qfContract);
            Assert.IsNotNull(contract);
            Assert.AreEqual("ES", contract.Symbol);
        }

        [TestMethod]
        public async Task GetFuturesOptionChainOk()
        {
            var qfShortOptionContract = (await _dbMktData.GetFuturesOptionContractAsync("ES20180615P2495"));
            var qfLongOptionContract = (await _dbMktData.GetFuturesOptionContractAsync("ES20180615P2395"));
            var contracts = await _mdApi.GetFuturesOptionSpreadAsync(qfShortOptionContract, qfLongOptionContract);
            Assert.IsNotNull(contracts);
            Assert.IsNotNull(contracts.shortContract);
            Assert.IsNotNull(contracts.longContract);
        }

        [TestMethod]
        public async Task GetFuturesPriceOk()
        {
            var qfContract = (await _dbMktData.GetFuturesContractAsync("ES20180615"));
            var contract = await _mdApi.GetFuturesContractAsync(RequestID.FuturesContract, qfContract);
            Assert.IsNotNull(contract);
            var tickData = _mdApi.GetFuturesPriceAsync(1234, contract);
            Assert.IsNotNull(tickData);
        }

        [TestMethod]
        public async Task GetFuturesOptionPriceOk()
        {
            var qfContract = (await _dbMktData.GetFuturesContractAsync("ES20180615"));
            var qfShortOptionContract = (await _dbMktData.GetFuturesOptionContractAsync("ES20180615P2495"));
            var qfLongOptionContract = (await _dbMktData.GetFuturesOptionContractAsync("ES20180615P2395"));
            var contract = await _mdApi.GetFuturesContractAsync(RequestID.FuturesContract, qfContract);
            Assert.IsNotNull(contract);
            var tickData = await _mdApi.GetFuturesPriceAsync(RequestID.Futures, contract);
            Assert.IsNotNull(tickData);
            var optionSpreadContracts = await _mdApi.GetFuturesOptionSpreadAsync(qfShortOptionContract, qfLongOptionContract);
            Assert.IsNotNull(optionSpreadContracts);
            var optTickData = default(FuturesOptionTickDataViewModel);
            await _mdApi.GetFuturesOptionPriceAsync(RequestID.ShortOption, optionSpreadContracts.shortContract, (e) => optTickData = e);
            Assert.IsNotNull(optTickData);
        }

    }

}
