using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.TradeOrder;
using TomasAI.IFM.Shared.TradeOrder.ViewModels;
using TomasAI.IFM.Shared.Fund;
using TomasAI.IFM.Shared.Fund.ViewModels;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;
using TomasAI.IFM.Shared.MarketDataAnalytics;

namespace TomasAI.IFM.Application.Storage.UnitTests.SqlServer
{
    public static class SampleData
    {
        public static VixFuturesEodDataReadModel VixFuturesEodData => new VixFuturesEodDataReadModel(
            ContractId: "ES20201218",
            ValueDate: new DateOnly(2018, 11, 18),
            OpenPrice: 3605,
            HighPrice: 3621,
            LowPrice: 3585.75,
            ClosePrice: 3620.75,
            Volume: 31285
        );

        public static FuturesTickDataViewModel VixFuturesTickData => new FuturesTickDataViewModel(
           ContractId: "VX20201218",
           ValueDate: new DateTime(2018, 11, 18),
           TickDate: new DateTime(2018, 11, 18),
           TickTime: 123456,
           Price: 3605,
           Size: 31285
        );

        public static FuturesContractV2ReadModel FuturesContract => new FuturesContractV2ReadModel
        (
            ContractId: "ES20190920",
            Description: "ES: e-mini S&P 500 futures 2019 Sep 20 @ GLOBEX",
            Symbol: "ES",
            LocalSymbol: "ESU9",
            SecurityType: "FUT",
            Currency: "USD",
            Exchange: "GLOBEX",
            Multiplier: "50",
            LastTradeDate: new DateTime(2019, 9, 20),
            CurrentlyTraded: true
        );

        public static FuturesTickDataViewModel[] FuturesTickData => new FuturesTickDataViewModel[]
        {
            new FuturesTickDataViewModel
            (
                ContractId: "ES20190920",
                TickDate: new DateTime(2019,8,8, 18,15,58,753),
                TickTime: 1565302556,
                Price: 2920,
                Size: 1,
                ValueDate: new DateTime(2019,8,9)
            )
        };

        public static FuturesOptionContractReadModel FuturesOptionContract => new FuturesOptionContractReadModel
        (
            ContractId: "ES20190816P2790",
            Description: string.Empty,
            Symbol: "ES",
            LocalSymbol: "EW3Q9",
            SecurityType: "FOP",
            Currency: "USD",
            Exchange: "GLOBEX",
            Multiplier: "50",
            ContractMonth: new DateTime(2019, 8, 16),
            StrikePrice: 2790,
            OptionType: "PUT"
        );


        public static FuturesOptionTickDataViewModel FuturesOptionTickData => new FuturesOptionTickDataViewModel
        (
            contractId: "ES20190315P2700",
            tickDate: new DateTime(2019, 3, 6),
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
            ContractId: "ES20190920",
            ValueDate: new DateTime(2019, 8, 15),
            OpenPrice: 2840,
            HighPrice: 2857.25,
            LowPrice: 2834.75,
            ClosePrice: 2846.75,
            Volume: 81398,
            DailyPercentChange: 0.002371125,
            DailyStdDev: 67.81488,
            UpperBand: 3076.049,
            Mean: 2940.419,
            LowerBand: 2804.789,
            MarketDirection: MarketDirectionType.Down,
            MarketVolatility: MarketVolatilityType.Normal,
            PriceDirection: PriceDirectionType.Flat,
            PriceVolatility: PriceVolatilityType.Falling,
            WindowSize: 0
        );

        public static TradePlanReadModel TradePlan => new TradePlanReadModel
        (
            TradePlanId: Guid.NewGuid(),
            OrderId: 1124,
            TradeId: 1117,
            TradeType: TradeType.ShortIronCondor,
            TradeDate: new DateTime(2019, 08, 15),
            ValueDate: new DateTime(2019, 08, 15),
            MaturityDate: new DateTime(2019, 08, 23),
            ActionDate: new DateTime(2019, 08, 15, 11, 57, 11, 77),
            ActionType: ActionType.HoldTradePosition,
            ActionSubType: ActionSubType.TradeInProfitPosition,
            ActionState: ActionState.Normal,
            ActionReason: "Market=>Down",
            TradePnl: 185.50m,
            ForwardLossRatio: 0.21,
            LossProbability: 0.1556439,
            MScore: 0.56,
            MaxProfit: 1314.25m,
            MaxLoss: -1314.25m,
            MinProfitTarget: 736.645m,
            DailyProfitTarget: 267.27m,
            AssetPrice: 2846.75m,
            AssetStdDev: 67.81488,
            AssetMean: 2940.419,
            AssetPriceChange: 0.45,
            MarketTrend: MarketDirectionType.Down,
            MarketVolatility: MarketVolatilityType.Normal,
            MarketDirection: PriceDirectionType.Rising,
            VixVolatility: PriceVolatilityType.Rising,
            TradeRisk: TradePlanReadModel.FromMScore(0.56),
            FiftyDayMA: 123.456,
            FiveDayXMA: 345.567,
            PutOTMProbability: 0.9356,
            CallOTMProbability: 0.7498,
            ShortPutGamma: 0.0005,
            ShortCallGamma: 0.0004,
            GammaRisk: GammaRiskType.HighShortPutGamma,
            NetPrice: 2.175m,
            ForwardPrice: 2.2439m,
            ForwardDelta: 0.50,
            StopLossLimit: 0.25,
            TrendType: FuturesTrendType.UpTrend,
            TrendStrength: FuturesTrendStrengthType.Medium,
            RSI: 45.67,
            RSISlope: 0.0245,
            TDI: FuturesTrendDirectionType.UpTrending,
            TDIStrength: FuturesTrendDirectionStrengthType.Medium,
            CreatedOn: new DateTime(2019, 8, 15, 11, 57, 11, 077),
            CreatedBy: "basil"
        );

        public static FundTransactionReadModel FundTransaction => new FundTransactionReadModel
        (
            TransactionDate: new DateTime(2019, 8, 19, 16, 20, 23, 307),
            TransactionType: FundTransactionType.UnrealizedTradePnl,
            FundId: 1004,
            OrderId: 1126,
            TradeId: 1121,
            TradeType: TradeType.ShortIronCondor,
            ValueDate: new DateTime(2019, 8, 9),
            TradeStatus: TradeStatus.EndOfDay,
            Description: "Fund Transaction Description",
            Amount: 166.25m,
            Balance: 9085.83m
        );



        public static YieldCurveRateReadModel[] YieldCurveRates => new YieldCurveRateReadModel[] {
            new YieldCurveRateReadModel(
                    ValueDate: new DateOnly(2018, 3, 6),
                    OneMonth: 2.45,
                    TwoMonth: 2.55,
                    ThreeMonth: 2.66,
                    SixMonth: 2.77,
                    OneYear: 2.78,
                    TwoYear: 2.88,
                    ThreeYear: 2.99,
                    FiveYear: 3.11,
                    SevenYear: 3.22,
                    TenYear: 3.33,
                    TwentyYear: 3.44,
                    ThirtyYear: 3.55
                ),
              new YieldCurveRateReadModel(
                    ValueDate: new DateOnly(2018, 3, 8),
                    OneMonth: 2.45,
                    TwoMonth: 2.55,
                    ThreeMonth: 2.66,
                    SixMonth: 2.77,
                    OneYear: 2.78,
                    TwoYear: 2.88,
                    ThreeYear: 2.99,
                    FiveYear: 3.11,
                    SevenYear: 3.22,
                    TenYear: 3.33,
                    TwentyYear: 3.44,
                    ThirtyYear: 3.55
                )

        };

        public static SpreadDistributionReadModel SpreadDistribution => new SpreadDistributionReadModel
        (
            Id: 894987,
            TradeId: 1154,
            TradeType: TradeType.PutCreditSpread,
            TradeStatus: TradeStatus.IntraDay,
            ValueDate: new DateOnly(2018, 3, 6),
            DaysToExpiry: 3,
            ForwardPrice: 0.8,
            LossProbability: 0.0,
            LossThreshold: 0,
            LossThresholdCount: 0,
            ShortVolatility: 0.2480247,
            LongVolatility: 0.2615045,
            ForwardLossRatio: 0.3145,
            CreatedOn: new DateTime(2019, 10, 12)
        );

        public static SpreadDistributionJobReadModel SpreadDistributionJob => new SpreadDistributionJobReadModel
        (
            JobId: 1234,
            OrderId: 5678,
            TradeId: 1234,
            TradeType: TradeType.ShortIronCondor,
            TradeStatus: TradeStatus.IntraDay,
            ValueDate: DateOnly.FromDateTime(new DateTime(2019, 10, 12)),
            DaysToExpiry: 5,
            OptionStyle: OptionStyle.American,
            OptionType: OptionType.Call,
            JobSubmitted: new DateTime(2019, 10, 12),
            JobStatus: "Job Status",
            JobCompleted: new DateTime(2019, 10, 12),
            JobFailed: new DateTime(2019, 10, 12),
            InProgress: true,
            LossProbabilityFactor: 0.23
        );

        public static FundOrderTradeReadModel FundOrderTrade => new FundOrderTradeReadModel
        (
            FundId: 1234,
            OrderId: 1002,
            TradeId: 1003,
            TradeType: TradeType.LongIronCondor,
            TradeDate:new DateTime(2019, 8, 13).Date,
            MaturityDate: new DateTime(2019, 9, 13).Date,
            TradeState: TradeState.TradeToClose,
            TradeAction: TradeAction.Buy,
            Reference: "Fund Order Trade Reference",
            PrimaryTrade: true,
            BaseContractSymbol: "ES",
            CreatedOn: new DateTime(2019, 8, 17),
            CreatedBy: "basil",
            UpdatedOn: new DateTime(2019, 8, 17),
            UpdatedBy: "basil"
        );

        public static EconomicCalendarReadModel EconomicCalendar => new EconomicCalendarReadModel
        (
            EventDate: new DateTime(2020, 10, 10),
            CountryCode: "US",
            EventName: "US Employment Report",
            Actual: "9000000",
            Forecast: "10000000",
            Prior: string.Empty,
            CreatedOn: new DateTime(2020, 10, 10),
            CreatedBy: "basil"
        );

        public static TradeOrderReadModel TradeOrder => new TradeOrderReadModel(
            FundId: 1234,
            OrderId: 2345,
            TradeId: 3456,
            ValueDate: new DateTime(2020, 12, 07),
            TradeType: TradeType.ShortIronCondor,
            TradeSubType: TradeSubType.Primary,
            TradeDate: new DateTime(2020, 12, 4),
            MaturityDate: new DateTime(2021, 01, 29),
            TradeOrderState: TradeOrderState.OrderOpened,
            UnderlyingContractId: "ES20201218",
            UnderlyingAssetType: AssetType.Futures,
            OrderDescription: "Test trade ticket",
            OrderAction: OrderAction.Sell,
            OrderActionType: OrderActionType.Open,
            OrderQuantity: 7,
            OrderFilled: 7,
            OrderType: OrderType.Limit,
            OrderPrice: 12.75m,
            OrderAmount: 1234.57m,
            Commission: 102.50m,
            TotalAmount: 1335.07m,
            TradePnl: 345.28m,
            TradeFillType: TradeFillType.Manual,
            CreatedOn: new DateTime(2020, 12, 06),
            CreatedBy: "basilt",
            UpdatedOn: new DateTime(2020, 12, 06),
            UpdatedBy: "basilt");

        public static YieldCurveRateReadModel YieldCurveRate = new YieldCurveRateReadModel(
            ValueDate: new DateOnly(2021, 10, 10),
            OneMonth: 2.45,
            TwoMonth: 2.55,
            ThreeMonth: 2.66,
            SixMonth: 2.77,
            OneYear: 2.78,
            TwoYear: 2.88,
            ThreeYear: 2.99,
            FiveYear: 3.11,
            SevenYear: 3.22,
            TenYear: 3.33,
            TwentyYear: 3.44,
            ThirtyYear: 3.55
        );
    }


}
