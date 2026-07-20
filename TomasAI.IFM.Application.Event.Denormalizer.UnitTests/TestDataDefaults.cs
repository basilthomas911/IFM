using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Fund;
using TomasAI.IFM.Shared.Fund.ViewModels;
using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;
using TomasAI.IFM.Shared.Reference;
using TomasAI.IFM.Shared.Reference.ViewModels;

namespace TomasAI.IFM.Application.Event.Denormalizer.UnitTests
{
    public static class TestDataDefaults
    { 
        public static OptionTradeViewModel OptionTrade => new OptionTradeViewModel
        (
            orderId: 1199,
            tradeId: 1276,
            tradeStrategy: string.Empty,
            tradeDate: new DateTime(2020,1,8),
            maturityDate: new DateTime(2020,1,17),
            tradeType: TradeType.IronCondor,
            tradeState: TradeState.TradeToOpen,
            tradeAction: TradeAction.Sell,
            underlyingContractId: "ES20200320",
            underlyingAssetType: AssetType.Futures,
            isPrimaryTrade: true,
            isHedgeTrade: false,
            createdOn: new DateTime(2020, 1, 8),
            createdBy: "basilt",
            updatedOn: new DateTime(2020, 1, 8),
            updatedBy: "basilt"
        );

        public static OptionLegReadModel OptionLeg => new OptionLegReadModel
        (
            tradeId: 1276,
            contractId: "ES20200117C3320",
            quantity: 38,
            strikePrice: 3320.00m,
            optionType: OptionType.Call,
            optionLegAction: OptionLegAction.Short,
            createdOn: new DateTime(2020,1,8),
            createdBy: "basilt",
            updatedOn: new DateTime(2020,1,8),
            updatedBy: "basilt"
        );

        public static TradeFillReadModel[] TradeFills => new TradeFillReadModel[]
        {
            new TradeFillReadModel
            (
                tradeId: 1038,
                contractId: "ES20181130P2600",
                fillDate: new DateTime(2018,11,15),
                price: 9.25m,
                quantity: 7,
                commission: 9.94m,
                createdOn: new DateTime(2018,11,15),
                createdBy: "basilt"
            )
        };

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

        public static FuturesTickDataViewModel FuturesTickData => new FuturesTickDataViewModel
            (
                contractId: "ES20190920",
                tickDate: new DateTime(2019, 8, 8, 18, 15, 58, 753),
                tickTime: 1565302556,
                price: 2920,
                size: 1,
                valueDate: new DateTime(2019, 8, 9)
            );

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
            dailyPercentChange: 0.002371125,
            dailyStdDev: 67.81488,
            upperBand: 3076.049,
            mean: 2940.419,
            lowerBand: 2804.789,
            marketTrend: MarketTrendType.Down,
            marketVolatility: MarketVolatilityType.Normal,
            marketDirection: MarketDirectionType.Flat,
            vixVolatility: VixVolatilityType.Falling,
            fiftyDayMA: 0.9356,
            twoHundredDayMA: 0.456
        );

        public static TradePlanReadModel TradePlan => new TradePlanReadModel
        (
            TradePlanId: Guid.NewGuid(),
            OrderId: 1124,
            TradeId: 1117,
            TradeType: TradeType.IronCondor,
            TradeDate: new DateTime(2019,08,15),
            ValueDate: new DateTime(2019, 08, 15),
            MaturityDate: new DateTime(2019, 08, 23),
            ActionDate: new DateTime(2019, 08, 15, 11, 57,11, 77),
            ActionType: ActionType.HoldTradePosition,
            ActionSubType: ActionSubType.TradeInProfitPosition,
            ActionState: ActionState.Normal,
            ActionReason: "Market=>Down",
            TradePnl: 185.50m,
            ForwardLossRatio: 0.21,
            LossProbability: 0.1556439,
            MScore: 0.73,
            MaxProfit: 1314.25m,
            MaxLoss: -1314.25m,
            MinProfitTarget: 736.645m,
            DailyProfitTarget: 267.27m,
            AssetPrice: 2846.75m,
            AssetStdDev: 67.81488,
            AssetMean: 2940.419,
            AssetPriceChange: 0.45,
            MarketTrend: MarketTrendType.Down,
            MarketVolatility: MarketVolatilityType.Normal,
            MarketDirection: MarketDirectionType.FallingSlowly,
            VixVolatility: VixVolatilityType.Falling,
            TradeRisk: TradePlanReadModel.FromMScore(0.73),
            FiftyDayMA: 45.5,
            TwoHundredDayMA: 100.45,
            PutOTMProbability: 0.8995341,
            CallOTMProbability: 0.7498,
            ShortPutGamma: 0.0005,
            ShortCallGamma: 0.0004,
            GammaRisk: GammaRiskType.HighShortPutGamma,
            NetPrice: 2.175m,
            ForwardPrice: 2.2439m,
            StopLossLimit: 0.25,
            CreatedOn: new DateTime(2019,8,15,11,57,11,077),
            CreatedBy: "basil"
        );

        public static FundTransactionReadModel FundTransaction => new FundTransactionReadModel
        (
            transactionId: 145,
            transactionDate: new DateTime(2019,8,19,16,20,23,307),
            transactionType: FundTransactionType.UnrealizedTradePnl,
            fundId: 1004,
            orderId: 1126,
            tradeId: 1121,
            tradeType: TradeType.IronCondor,
            valueDate: new DateTime(2019,8,9),
            tradeStatus: TradeStatus.EndOfDay,
            description: "Fund Transaction Description",
            amount: 166.25m,
            balance: 9085.83m
        );

        public static YieldCurveRateReadModel YieldCurveRate => new YieldCurveRateReadModel(
                    valueDate: new DateTime(2018,3,6),
                    oneMonth: 2.45,
                    twoMonth: 2.55,
                    threeMonth: 2.66,
                    sixMonth: 2.77,
                    oneYear: 2.78,
                    twoYear: 2.88,
                    threeYear: 2.99,
                    fiveYear: 3.11,
                    sevenYear: 3.22,
                    tenYear: 3.33,
                    twentyYear: 3.44,
                    thirtyYear: 3.55
                );

        public static YieldCurveRateReadModel[] YieldCurveRates => new YieldCurveRateReadModel[] {
            new YieldCurveRateReadModel(
                    valueDate: new DateTime(2018, 3, 6),
                    oneMonth: 2.45,
                    twoMonth: 2.55,
                    threeMonth: 2.66,
                    sixMonth: 2.77,
                    oneYear: 2.78,
                    twoYear: 2.88,
                    threeYear: 2.99,
                    fiveYear: 3.11,
                    sevenYear: 3.22,
                    tenYear: 3.33,
                    twentyYear: 3.44,
                    thirtyYear: 3.55
                ),
              new YieldCurveRateReadModel(
                    valueDate: new DateTime(2018, 3, 8),
                    oneMonth: 2.45,
                    twoMonth: 2.55,
                    threeMonth: 2.66,
                    sixMonth: 2.77,
                    oneYear: 2.78,
                    twoYear: 2.88,
                    threeYear: 2.99,
                    fiveYear: 3.11,
                    sevenYear: 3.22,
                    tenYear: 3.33,
                    twentyYear: 3.44,
                    thirtyYear: 3.55
                )

        };

        public static TradePositionReadModel TradePosition => new TradePositionReadModel
        (
            orderId: 1124,
            tradeId: 1278,
            tradeType: TradeType.PutCreditSpread,
            valueDate: new DateTime(2020,1,10),
            daysToExpiry: 11,
            tradeStatus: TradeStatus.Open,
            commission: 85.20m,
            deltaHedge: 0,
            netSpread: 1.48m,
            tradeValue: 2212.50m,
            tradePnl: 0m,
            assetPrice: 3280.75m,
            otmProbability: 0.879407,
            forwardPrice: 0m,
            forwardLossRatio: 0.67,
            lossProbability: 0.0,
            riskFreeRate: 0.0155,
            createdOn: new DateTime(2020,1,10),
            createdBy: "basilt",
            updatedOn: new DateTime(2020,1,10),
            updatedBy: "basilt"
        );

        public static TradeLimitReadModel TradeLimit => new TradeLimitReadModel
        (
            tradeId: 1278,
            tradeType: TradeType.IronCondor,
            riskMargin: 46116.00m,
            maxProfit: 3187.50m,
            maxLoss: -3187.50m,
            maxReturn: 0.05866949,
            maxLossLimit: 4.25,
            minProfitLimit: 0.53125,
            minProfitTarget: 1934.55m,
            dailyProfitTarget: 739.2375m,
            createdOn: new DateTime(2020, 1, 10),
            createdBy: "basilt",
            updatedOn: new DateTime(2020, 1, 10),
            updatedBy: "basilt"
        );

        public static TradeTypeLimitReadModel TradeTypeLimit => new TradeTypeLimitReadModel
        (
            tradeId: 1278,
            tradeType: TradeType.IronCondor,
            maxLossLimit: -1000,
            minProfitLimit: 1000
        );

        public static SpreadDistributionReadModel SpreadDistribution => new SpreadDistributionReadModel
        {
            Id = 894987,
            TradeId = 1154,
            TradeType = TradeType.PutCreditSpread,
            TradeStatus = TradeStatus.IntraDay,
            ValueDate = new DateTime(2018,3,6),
            DaysToExpiry = 3,
            ShortVolatility = 0.2480247,
            LongVolatility = 0.2615045,
            ForwardPrice = 0.8,
            LossProbability = 0.45,
            LossThreshold = -1245,
            LossThresholdCount = 2,
            CreatedOn = new DateTime(2019,10,12)
        };

        public static StrikePriceVolatilityReadModel StrikePriceVolatility => new StrikePriceVolatilityReadModel(
            symbol: "ES",
            tradeType: TradeType.IronCondor,
            marketTrend: MarketTrendType.Up,
            marketVolatility: MarketVolatilityType.Normal,
            marketVolatilityTrend: MarketDirectionType.Rising,
            delta: 11,
            strikePriceOffset: 2
            );

        public static StrikePriceVolatilityReadModel StrikePriceVolatilityChanged => new StrikePriceVolatilityReadModel(
            symbol: "ES",
            tradeType: TradeType.IronCondor,
            marketTrend: MarketTrendType.Down,
            marketVolatility: MarketVolatilityType.Normal,
            marketVolatilityTrend: MarketDirectionType.Rising,
            delta: 11,
            strikePriceOffset: 2
            );

        public static SpreadDistributionJobReadModel SpreadDistributionJob => new SpreadDistributionJobReadModel
        {
            JobId = 1234,
            OrderId = 5678,
            TradeId = 1234,
            TradeType = TradeType.IronCondor,
            TradeStatus = TradeStatus.IntraDay,
            ValueDate = new DateTime(2019, 10, 12),
            DaysToExpiry = 5,
            OptionStyle = OptionStyle.American,
            OptionType = OptionType.Call,
            JobSubmitted = new DateTime(2019, 10, 12),
            JobStatus = "Job Status",
            JobCompleted = new DateTime(2019, 10, 12),
            JobFailed = new DateTime(2019, 10, 12),
            InProgress = true,
            CallSpreadDistribution = null,
            PutSpreadDistribution = null,
            Duration = 0.44,
            LossProbabilityFactor = 0.23
        };

        public static FundOrderTradeReadModel FundOrderTrade => new FundOrderTradeReadModel
        (
            orderId: 1002,
            tradeId: 1003,
            tradeType: TradeType.ShortIronCondor,
            tradeDate: new DateTime(2019, 8, 13),
            maturityDate: new DateTime(2019, 9, 13),
            tradeState: TradeState.TradeToClose,
            tradeAction: TradeAction.Buy,
            reference: "Fund Order Trade Reference",
            createdOn: new DateTime(2019, 8, 17),
            createdBy: "basil",
            primaryTrade: true,
            baseContractSymbol: "ES"
        );
    }
}
