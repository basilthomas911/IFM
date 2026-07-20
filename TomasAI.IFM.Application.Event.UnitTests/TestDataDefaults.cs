using System;
using System.Collections.Generic;
using System.Data;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Fund;
using TomasAI.IFM.Shared.Fund.ViewModels;
using TomasAI.IFM.Shared.Util;

namespace TomasAI.IFM.Application.Event.UnitTests
{
    public static class TestDataDefaults
    {
        public static FuturesContractViewModel FuturesContract => new FuturesContractViewModel
        (
            contractId: "ES20190920",
            description: "ES: e-mini S&P 500 futures 2019 Sep 20 @ GLOBEX",
            symbol: "ES",
            localSymbol: "ESU9",
            securityType: "FUT",
            currency: "USD",
            exchange: "GLOBEX",
            multiplier: "50",
            lastTradeDate: new DateTime(2019,9,20),
            currentlyTraded: true
        );

        public static FuturesTickDataViewModel[] FuturesTickData => new FuturesTickDataViewModel[]
        {
            new FuturesTickDataViewModel
            (
                contractId: "ES20190920",
                tickDate: new DateTime(2019,8,8, 18,15,58,753),
                tickTime: 1565302556,
                price: 2920,
                size: 1,
                valueDate: new DateTime(2019,8,9)
            )
        };

        public static FuturesOptionContractReadModel FuturesOptionContract => new FuturesOptionContractReadModel
        (
            contractId: "ES20190816P2790",
            description: string.Empty,
            symbol: "ES",
            localSymbol: "EW3Q9",
            securityType: "FOP",
            currency: "USD",
            exchange: "GLOBEX",
            multiplier: "50",
            contractMonth: new DateTime(2019,8,16),
            strikePrice: 2790,
            optionType: "PUT"
        );

        
        public static FuturesOptionTickDataViewModel FuturesOptionTickData => new FuturesOptionTickDataViewModel
        (
            contractId: "ES20190315P2700",
            tickDate: new DateTime(2019,3,6),
            tickTime: 1551881144,
            optionPrice: 4,
            bidPrice: 3.95,
            askPrice: 4.05,
            bidSize: 1,
            askSize: 308,
            impliedVolatility: 0.1750748,
            delta: -0.1073284,
            gamma: 0.002405739,
            vega: 0.8091136,
            theta: -279.6482,
            rho: -0.07134622,
            underlyingPrice: 2791
        );

        public static FuturesEodDataViewModel FuturesEodData => new FuturesEodDataViewModel
        (
            contractId: "ES20190920",
            valueDate: new DateTime(2019, 8, 15),
            openPrice: 2840,
            highPrice: 2857.25,
            lowPrice: 2834.75,
            closePrice: 2846.75,
            volume: 81398,
            size: 10,
            dailyPercentChange: 0.002371125,
            dailyStdDev: 67.81488,
            upperBand: 3076.049,
            mean: 2940.419,
            lowerBand: 2804.789,
            marketTrend: MarketTrendType.Down,
            marketVolatility: MarketVolatilityType.Normal,
            tradeStrategy: "Call Credit Spread @ -1.15 SDEV",
            putSpreadProbability: 0.9356,
            putSpreadStdDev: 1.845311,
            callSpreadProbability: 0.7498,
            callSpreadStdDev: 1.154689,
            rateOfReturn: 0.1031409,
            nearestPutStrike: 2720,
            nearestCallStrike: 2925
        );

        public static TradePlanReadModel TradePlan => new TradePlanReadModel
        (
            orderId: 1124,
            tradeId: 1117,
            tradeType: TradeType.IronCondor,
            tradeDate: new DateTime(2019,08,15),
            valueDate: new DateTime(2019, 08, 15),
            maturityDate: new DateTime(2019, 08, 23),
            actionDate: new DateTime(2019, 08, 15, 11, 57,11, 77),
            actionType: TradePlanActionType.HoldTradePosition,
            actionState: TradePlanActionState.Normal,
            actionReason: "Market=>Down",
            tradePnl: 185.50m,
            forwardLossRatio: 0.34,
            lossProbability: 0.1556439,
            maxProfit: 1314.25m,
            maxLoss: -1314.25m,
            maxTradePnl: 0.0m,
            minProfitTarget: 736.645m,
            dailyProfitTarget: 267.27m,
            assetPrice: 2846.75m,
            assetStdDev: 67.81488,
            assetMean: 2940.419,
            assetPriceChange: 0.45,
            intradayVolatility: 0.23,
            marketTrend: MarketTrendType.Down,
            marketVolatility: MarketVolatilityType.Normal,
            putSpreadStdDev: 1.845311,
            putSpreadProbability: 0.9356,
            putSpreadActualProbability: 0.8995341,
            callSpreadStdDev: 1.154689,
            callSpreadProbability: 0.7498,
            callSpreadActualProbability: 0.9304676,
            netPrice: 2.175m,
            forwardPrice: 2.2439m,
            stopLossLimit: 0.25,
            createdOn: new DateTime(2019,8,15,11,57,11,077),
            createdBy: "basil"
        );

        
        public static FuturesEodDataViewModel[] FuturesEodDataValues => ReadFuturesEodDtatFromCsv();

        public static FundOrderReadModel FundOrder => new FundOrderReadModel(
            fundId: 1001,
            orderId: 1001,
            orderDate: new DateTime(2018,11,15),
            orderStatus: OrderStatus.Open,
            reference: "IronCondor: ES20181221 @ Nov 30",
            createdOn: new DateTime(2018, 11, 15),
            createdBy: "basilt",
            updatedOn: new DateTime(2018, 11, 15),
            updatedBy: "basilt"
        );

        public static FundOrderTradeReadModel FundOrderTrade => new FundOrderTradeReadModel
        (
            orderId: 2002,
            tradeId: 3003,
            tradeType: TradeType.IronCondor,
            tradeDate: new DateTime(2019, 10, 10),
            maturityDate: new DateTime(2019, 10, 20),
            tradeState: TradeState.NewTrade,
            tradeAction: TradeAction.Buy,
            reference: "FundOrderTrade Reference",
            primaryTrade: true,
            createdOn: DateTime.Now,
            createdBy: "basilt"
        );

        public static FundTransactionReadModel FundTransaction(FundTransactionType fundTransactionType) => new FundTransactionReadModel
        (
             transactionId: 145,
             transactionDate: new DateTime(2019, 8, 19, 16, 20, 23, 307),
             transactionType: fundTransactionType,
             fundId: 1004,
             orderId: 1126,
             tradeId: 1121,
             tradeType: TradeType.IronCondor,
             valueDate: new DateTime(2019, 8, 9),
             tradeStatus: TradeStatus.EndOfDay,
             description: "Fund Transaction Description",
             amount: 166.25m,
             balance: 9085.83m
         );

        private static FuturesEodDataViewModel[] ReadFuturesEodDtatFromCsv()
        {
            var futuresEodData = new List<FuturesEodDataViewModel>();
            var testFile = @"C:\TomasAI\Projects\IFM\TomasAI.InvestmentFundManager\TomasAI.IFM.Shared.UnitTests\TestData\FuturesEodData-TestData.csv";
            var dr = new CsvDataReader(testFile) as IDataReader;
            while(dr.Read())
            {
                futuresEodData.Add(new FuturesEodDataViewModel
                (
                    contractId: dr.GetString(0),
                    valueDate: dr.GetDateTime(1),
                    openPrice: dr.GetDouble(2),
                    highPrice: dr.GetDouble(3),
                    lowPrice: dr.GetDouble(4),
                    closePrice: dr.GetDouble(5),
                    volume: dr.GetInt32(6),
                    size: dr.GetInt32(7),
                    dailyPercentChange: dr.GetDouble(8),
                    dailyStdDev: dr.GetDouble(9),
                    upperBand: dr.GetDouble(11),
                    mean: dr.GetDouble(12),
                    lowerBand: dr.GetDouble(13),
                    marketTrend: (MarketTrendType)(Enum.Parse(typeof(MarketTrendType), dr.GetString(14))),
                    marketVolatility: MarketVolatilityType.Normal,
                    tradeStrategy: dr.GetString(15),
                    putSpreadProbability: dr.GetDouble(16),
                    putSpreadStdDev: dr.GetDouble(17),
                    callSpreadProbability: dr.GetDouble(18),
                    callSpreadStdDev: dr.GetDouble(19),
                    rateOfReturn: dr.GetDouble(20),
                    nearestPutStrike: dr.GetInt32(21),
                    nearestCallStrike: dr.GetInt32(22)
                ));
            }
            return futuresEodData.ToArray();
        }
    }
}
