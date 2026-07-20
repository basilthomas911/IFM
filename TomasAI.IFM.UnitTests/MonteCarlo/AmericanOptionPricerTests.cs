using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MonteCarloOptionPricer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Storage.OptionPricerDb;
using TomasAI.IFM.Domain.OptionPricer.Model;
using TomasAI.IFM.Service.MarketDataFeed.InteractiveBrokers;

namespace TomasAI.IFM.UnitTest.MonteCarlo
{
    [TestClass] 
    public class AmericanOptionPricerTests
    {
        /*
        [TestMethod]

        
        public async Task IronCondorSpreadDistributionOk()
        {
            // 1228:1383
            var tradeDate = new DateTime(2020, 03, 26);
            var valueDate = new DateTime(2020, 03, 27);
            var maturityDate = new DateTime(2020, 05, 29);
            var dbConn = new DbConnectionSettings()
                  .Add("MarketDataDbConnection", "Data Source=DEV-SERVER;Initial Catalog=marketdatadb;Integrated Security=True;MultipleActiveResultSets=True", "System.Data.SqlClient")
                  .Add("TradeDbConnection", "Data Source=DEV-SERVER;Initial Catalog=tradedb;Integrated Security=True;MultipleActiveResultSets=True", "System.Data.SqlClient")
                  .Add("OptionPricerDbConnection", "Data Source=DEV-SERVER;Initial Catalog=optionpricerdb;Integrated Security=True;MultipleActiveResultSets=True", "System.Data.SqlClient");
            var diContainer = new Dictionary<Type, IObjectRepository>();
            var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
            var dbFactory = new DbContextFactory(dbResolver);
            diContainer.Add(typeof(IObjectRepository<MarketDataDbContext>), new MarketDataDbContext(dbConn, dbFactory));
            diContainer.Add(typeof(IObjectRepository<OptionPricerDbContext>), new OptionPricerDbContext(dbConn, dbFactory));
            var dbMktData = dbFactory.MarketDataDb as MarketDataDbContext; var qfFuturesContract = (await dbMktData.GetFuturesContractAsync("ES20200619"));
            var pcsShortOptionContract = await dbMktData.GetFuturesOptionContractAsync("ES20200529P1850");
            var pcsLongOptionContract = await dbMktData.GetFuturesOptionContractAsync("ES20200529P1790");
            var ccsShortOptionContract = await dbMktData.GetFuturesOptionContractAsync("ES20200529C3070");
            var ccsLongOptionContract = await dbMktData.GetFuturesOptionContractAsync("ES20200529C3110");
            var riskFreeRate = (await dbMktData.GetLastYieldCurveRateAsync()).TwoMonth/100;

            //var rateOfReturn = (await dbMktData.GetRateOfReturnAsync("ES", valueDate)).RateOfReturn;
            var rateOfReturn = riskFreeRate;
            var daysToMaturity = (await dbMktData.GetTradingDaysAsync(valueDate, maturityDate)); 
            var tradingDays = (int)(maturityDate - tradeDate).TotalDays;

            var dbOp = dbFactory.OptionPricerDb as OptionPricerDbContext;
            var gpuDevices = await dbOp.GetOptionPricerDevicesAsync();
            var devices = new OptionPricerDeviceCollection(gpuDevices.ToList());
            var optionPricerFactory = new OptionPricerFactory(devices);
            var optCalc = new OptionCalculator(valueDate, maturityDate);

            var assetPrice = 2524.00;
            var shortPutBid = 46.75;
            var shortPutAsk = 49.75;
            var longPutBid = 39.75;
            var longPutAsk = 42.75;
            var shortCallBid = 11.50;
            var shortCallAsk = 12.75;
            var longCallBid = 8.25;
            var longCallAsk = 9.50;

            var shortPutGreeks = optCalc.GetOptionGreeks(OptionTypeName.Put, assetPrice, pcsShortOptionContract.StrikePrice, (shortPutBid + shortPutAsk) / 2, riskFreeRate);
            var longPutGreeks = optCalc.GetOptionGreeks(OptionTypeName.Put, assetPrice, pcsLongOptionContract.StrikePrice, (longPutBid + longPutAsk) / 2, riskFreeRate);
  
            var pcsArgs = new CreditSpreadPricerArgs
            (
                TradeId: 1234,
                TradeType: TradeType.ShortIronCondor,
                TradeStatus: TradeStatus.IntraDay,
                ValueDate: DateTime.Now.Date,
                OptionStyle: OptionStyle.American,
                OptionType: OptionType.Put,
                DaysToMaturity: daysToMaturity,
                AssetPrice: Convert.ToDecimal(assetPrice),
                RiskFreeRate: riskFreeRate,
                ShortBid: Convert.ToDecimal(shortPutBid),
                ShortAsk: Convert.ToDecimal(shortPutAsk),
                ShortStrike: pcsShortOptionContract.StrikePrice,
                ShortImpliedVolatility: shortPutGreeks.ImpliedVolatility,
                LongBid: Convert.ToDecimal(longPutBid),
                LongAsk: Convert.ToDecimal(longPutAsk),
                LongStrike: pcsLongOptionContract.StrikePrice,
                LongImpliedVolatility: longPutGreeks.ImpliedVolatility,
                RateOfReturn: rateOfReturn
            );

            var shortCallGreeks = optCalc.GetOptionGreeks(OptionTypeName.Call, assetPrice, ccsShortOptionContract.StrikePrice, (shortCallBid + shortCallAsk) / 2, riskFreeRate);
            var longCallGreeks = optCalc.GetOptionGreeks(OptionTypeName.Call, assetPrice, ccsLongOptionContract.StrikePrice, (longCallBid + longCallAsk) / 2, riskFreeRate);
  
            var ccsArgs = new CreditSpreadPricerArgs
            (
                TradeId: 1234,
                TradeType: TradeType.ShortIronCondor,
                TradeStatus: TradeStatus.IntraDay,
                ValueDate: DateTime.Now.Date,
                OptionStyle: OptionStyle.American,
                OptionType: OptionType.Call,
                DaysToMaturity: daysToMaturity,
                AssetPrice: Convert.ToDecimal(assetPrice),
                RiskFreeRate: riskFreeRate,
                ShortBid: Convert.ToDecimal(shortCallBid),
                ShortAsk: Convert.ToDecimal(shortCallAsk),
                ShortStrike: ccsShortOptionContract.StrikePrice,
                ShortImpliedVolatility: shortCallGreeks.ImpliedVolatility,
                LongBid: Convert.ToDecimal(longCallBid),
                LongAsk: Convert.ToDecimal(longCallAsk),
                LongStrike: ccsLongOptionContract.StrikePrice,
                LongImpliedVolatility: longCallGreeks.ImpliedVolatility,
                RateOfReturn: rateOfReturn
            );

            var dbTrade = dbFactory.TradeDb as TradeDbContext;
            var optionTrade = (await dbTrade.GetOptionTradeAsync(1228,1383));
            var pcs = optionTrade.TradePositions.Get(TradeType.PutCreditSpread, TradeStatus.EndOfDay);
            var ccs = optionTrade.TradePositions.Get(TradeType.CallCreditSpread, TradeStatus.EndOfDay);
            pcsArgs.LossFactor = pcs.OTMProbability > ccs.OTMProbability ? 0 : 1;
            ccsArgs.LossFactor = pcsArgs.LossFactor == 1 ? 0 : 1;

            var csPricer = new OptionSpreadPricer(optionPricerFactory);
            var csParams = csPricer.PriceIronCondor(pcsArgs, ccsArgs);
            var futuresEodData = (await dbMktData.GetFuturesEodDataAsync(qfFuturesContract.ContractId, valueDate));
            var intradayStdDev = (futuresEodData.HighPrice - futuresEodData.LowPrice) / futuresEodData.DailyStdDev;

            var spreadConvergence = Math.Abs(pcs.OTMProbability - ccs.OTMProbability);
            var totalDelta = Math.Abs(shortPutGreeks.Delta + longPutGreeks.Delta + shortCallGreeks.Delta + longCallGreeks.Delta);
            var putDelta = pcsArgs.LossFactor == 1
                ? spreadConvergence
                : Math.Abs(shortPutGreeks.Delta);
            var callDelta = ccsArgs.LossFactor == 1
                ? spreadConvergence
                : Math.Abs(shortCallGreeks.Delta);

            var quantity = optionTrade.OptionLegs.GetQuantity(OptionLegAction.Short, OptionType.Put).Value;
            var maxLoss = Convert.ToDouble(optionTrade.TradeLimit.MaxLoss);
            var tradePnl = optionTrade.TradePositions.GetTradePnl();
            optionPricerFactory.Clear();
        }
        */

        [TestMethod]
        public void PricePutOptionOk()
        {
            var startDate = new DateTime(2018, 9, 20);
            var endDate = new DateTime(2018, 9, 28);
            var connName = "MarketDataDbConnection";
            var connString = "Data Source=DEV-SERVER;Initial Catalog=marketdatadb;Integrated Security=True;MultipleActiveResultSets=True";
            var provName = "System.Data.SqlClient";
            var dbConn = new DbConnectionSettings()
                           .Add(connName, connString, provName);
            var diContainer = new Dictionary<Type, MarketDataDbContext>();
            var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
            var dbFactory = new DbContextFactory(dbResolver);
            diContainer.Add(typeof(IObjectRepository<MarketDataDbContext>), new MarketDataDbContext(dbConn, dbFactory));
            var db = dbFactory.MarketDataDb as MarketDataDbContext;
            var tradingDays = db.GetTradingDaysAsync(startDate, endDate).Result;
            var optionPricerId = new OptionPricerId(OptionStyle.American, OptionType.Put, 0, 1 << 19, 1 << 16, tradingDays, 4096);
            using (var pricer = new AmericanPutOptionPricer(optionPricerId))
            {
                //tradingDays--;
                var assetPrice = 2916.75;
                var impliedVol = 0.1030;
                var value = pricer.PriceOptionAsync(
                        1 << 19,
                        tradingDays,
                        0.1216,
                        impliedVol,
                        assetPrice,
                        2885.00,
                        0.0203).Result;
            }
        }

        [TestMethod]
        public void PriceCallOptionOk()
        {
            var startDate = new DateTime(2018, 9, 20);
            var endDate = new DateTime(2018, 9, 28);
            var dbConn = new DbConnectionSettings()
              .Add("MarketDataDbConnection", @"Data Source=DEV-SERVER;Initial Catalog=marketdatatestdb;Integrated Security=True;MultipleActiveResultSets=True", "System.Data.SqlClient");
            var diContainer = new Dictionary<Type, MarketDataDbContext>();
            var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
            var dbFactory = new DbContextFactory(dbResolver);
            diContainer.Add(typeof(IObjectRepository<MarketDataDbContext>), new MarketDataDbContext(dbConn, dbFactory));
            var db = dbFactory.MarketDataDb as MarketDataDbContext;

            var tradingDays = db.GetTradingDaysAsync(startDate, endDate).Result;
            var optionPricerId = new OptionPricerId(OptionStyle.American, OptionType.Call, 0, 1 << 19, 1 << 16, tradingDays, 4096);
            using (var pricer = new AmericanCallOptionPricer(optionPricerId))
            {
                var assetPrice = 2916.25;
                var impliedVol = 0.0742;
                var value = pricer.PriceOptionAsync(
                        1 << 19,
                        tradingDays,
                        0.1216,
                        impliedVol,
                        assetPrice,
                        3015.00,
                        0.0203).Result;
            }
        }

        

    
        [TestMethod]
        public async Task FindPutOptionSpreadContractsOk()
        {
            var dbConn = new DbConnectionSettings()
                 .Add("MarketDataDbConnection", "Data Source=DEV-SERVER;Initial Catalog=marketdatadb;Integrated Security=True;MultipleActiveResultSets=True", "System.Data.SqlClient")
                 .Add("OptionPricerDbConnection", "Data Source=DEV-SERVER;Initial Catalog=optionpricerdb;Integrated Security=True;MultipleActiveResultSets=True", "System.Data.SqlClient");
              var diContainer = new Dictionary<Type, IObjectRepository>();
            var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
            var dbFactory = new DbContextFactory(dbResolver);
            diContainer.Add(typeof(IObjectRepository<MarketDataDbContext>), new MarketDataDbContext(dbConn, dbFactory));
            diContainer.Add(typeof(IObjectRepository<OptionPricerDbContext>), new OptionPricerDbContext(dbConn, dbFactory));

            var dbMktData = dbFactory.MarketDataDb as MarketDataDbContext; 
            var qfFuturesContract = (await dbMktData.GetFuturesContractAsync("ES180615"));
            var qfShortOptionContract = (await dbMktData.GetFuturesOptionContractAsync("ES20180511P2575"));
            var qfLongOptionContract = (await dbMktData.GetFuturesOptionContractAsync("ES20180511P2475"));

            var options = new IBMarketDataApiOptions("127.0.0.1", 7496, 1);
            var mdApi = new IBMarketDataApi(options,null, null);
            mdApi.Start();
            var futuresContract = await mdApi.GetFuturesContractAsync(RequestID.FuturesContract, qfFuturesContract);
            var tickData = await mdApi.GetFuturesPriceAsync(RequestID.Futures, futuresContract);
            var assetPrice = tickData.Price;
            var contractMonth = $"{new DateTime(2018, 03, 16):yyyyMMdd}";
            var optionSpread = await mdApi.GetFuturesOptionSpreadAsync(qfShortOptionContract, qfLongOptionContract);
            Assert.IsNotNull(optionSpread);
            var shortOption = default(FuturesOptionTickDataViewModel);
            await mdApi.GetFuturesOptionPriceAsync(RequestID.ShortOption, optionSpread.shortContract, e => shortOption = e);
            var longOption = default(FuturesOptionTickDataViewModel);
            await mdApi.GetFuturesOptionPriceAsync(RequestID.LongOption, optionSpread.longContract, e => longOption = e);
            mdApi.Stop();
            Assert.IsNotNull(shortOption);
            Assert.IsNotNull(longOption);
        }

        [TestMethod]
        public async Task FindCallOptionSpreadContractsOk()
        {
            var dbConn = new DbConnectionSettings()
                 .Add("MarketDataDbConnection", "Data Source=DEV-SERVER;Initial Catalog=marketdatadb;Integrated Security=True;MultipleActiveResultSets=True", "System.Data.SqlClient")
                 .Add("OptionPricerDbConnection", "Data Source=DEV-SERVER;Initial Catalog=optionpricerdb;Integrated Security=True;MultipleActiveResultSets=True", "System.Data.SqlClient");
            var diContainer = new Dictionary<Type, IObjectRepository>();
            var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
            var dbFactory = new DbContextFactory(dbResolver);
            diContainer.Add(typeof(IObjectRepository<MarketDataDbContext>), new MarketDataDbContext(dbConn, dbFactory));
            diContainer.Add(typeof(IObjectRepository<OptionPricerDbContext>), new OptionPricerDbContext(dbConn, dbFactory));
            var dbMktData = dbFactory.MarketDataDb as MarketDataDbContext;

            var qfFuturesContract = (await dbMktData.GetFuturesContractAsync("ES180615"));
            var qfShortOptionContract = (await dbMktData.GetFuturesOptionContractAsync("ES20180511P2575"));
            var qfLongOptionContract = (await dbMktData.GetFuturesOptionContractAsync("ES20180511P2475"));

            var options = new IBMarketDataApiOptions("127.0.0.1", 7496, 1);
            var mdApi = new IBMarketDataApi(options, null, null);
            mdApi.Start();
            var futuresContract = await mdApi.GetFuturesContractAsync(RequestID.FuturesContract, qfFuturesContract);
            var tickData = mdApi.GetFuturesPriceAsync(RequestID.Futures, futuresContract).Result;
            var assetPrice = tickData.Price;
            var contractMonth = new DateTime(2018, 03, 16).ToString("yyyyMMdd");
            var optionSpread = await mdApi.GetFuturesOptionSpreadAsync(qfShortOptionContract, qfLongOptionContract);
            Assert.IsNotNull(optionSpread);
            var shortOption = default(FuturesOptionTickDataViewModel);
            await mdApi.GetFuturesOptionPriceAsync(RequestID.ShortOption, optionSpread.shortContract, e =>shortOption = e );
            var longOption = default(FuturesOptionTickDataViewModel);
            await mdApi.GetFuturesOptionPriceAsync(RequestID.LongOption, optionSpread.longContract, e => longOption = e);
            mdApi.Stop();
            Assert.IsNotNull(shortOption);
            Assert.IsNotNull(longOption);
        }
    }
}
