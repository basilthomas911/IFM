using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.TradeOrder.ViewModels;
using TomasAI.IFM.Shared.TradeOrder;

namespace TomasAI.IFM.Application.Storage.UnitTests.ScyllaDb.TradeDb;

public class SampleData
{
    private const int OrderId = 1001;
    private const int TradeId = 2001;
    private static readonly DateOnly DefaultValueDate = new DateOnly(2025, 3, 15);
    private static readonly DateTime DefaultCreatedOn = new DateTime(2025, 3, 15, 9, 30, 0);
    private static readonly string DefaultUser = "testuser";

    // Set up relationships between the data objects
    static SampleData()
    {
        // Set up option legs for option leg data
        foreach (var optionLegData in OptionLegsData)
        {
            optionLegData.SetOptionLeg(OptionLegs.FirstOrDefault(leg => leg.ContractId == optionLegData.OptionLegId));
        }

        // Add option leg data to trade positions
        foreach (var position in TradePositions)
        {
            // Add appropriate option leg data based on TradeType
            var relevantLegData = OptionLegsData
                .Where(leg => leg.TradeType == position.TradeType &&
                              leg.DaysToExpiry == position.DaysToExpiry &&
                              leg.TradeStatus == position.TradeStatus)
                .ToList();

            // If no specific leg data for this type, use all legs for LongIronCondor
            if (!relevantLegData.Any() && position.TradeType != TradeType.LongIronCondor)
            {
                relevantLegData = OptionLegsData
                    .Where(leg => leg.DaysToExpiry == position.DaysToExpiry &&
                                 leg.TradeStatus == position.TradeStatus)
                    .ToList();
            }

            position.AddOptionLegData(relevantLegData);
        }

        // Add trade fill data to trade fills
        TradeFills[0].AddTradeFillData(TradeFillData.ToArray());

        // Set up the complete option trade
        OptionTrade
            .AddOptionLegs(OptionLegs)
            .AddTradePosition(TradePositions)
            .SetTradeLimit(TradeLimit)
            .AddTradeTypeLimits(TradeTypeLimits)
            .AddTradeFills(TradeFills);
    }

    // Base OptionTrade
    public static readonly OptionTradeReadModel OptionTrade = new OptionTradeReadModel(
        orderId: OrderId,
        tradeId: TradeId,
        tradeStrategy: "IronCondor",
        tradeDate: DefaultValueDate,
        maturityDate: new DateOnly(2025, 6, 18),
        tradeType: TradeType.LongIronCondor,
        tradeState: TradeState.TradeToOpen,
        tradeAction: TradeAction.Buy,
        underlyingContractId: "ES202506",
        underlyingAssetType: AssetType.Futures,
        isPrimaryTrade: true,
        isHedgeTrade: false,
        createdOn: DefaultCreatedOn,
        createdBy: DefaultUser,
        updatedOn: DefaultCreatedOn.AddMinutes(45),
        updatedBy: DefaultUser);

    // Secondary trade for testing collections
    public static readonly OptionTradeReadModel SecondaryTrade = new OptionTradeReadModel(
        orderId: OrderId,
        tradeId: 2002,
        tradeStrategy: "IronCondor",
        tradeDate: DefaultValueDate,
        maturityDate: new DateOnly(2025, 6, 18),
        tradeType: TradeType.ShortIronCondor,
        tradeState: TradeState.TradeToClose,
        tradeAction: TradeAction.Sell,
        underlyingContractId: "ES202506",
        underlyingAssetType: AssetType.Futures,
        isPrimaryTrade: false,
        isHedgeTrade: true,
        createdOn: DefaultCreatedOn.AddMinutes(15),
        createdBy: DefaultUser,
        updatedOn: DefaultCreatedOn.AddMinutes(50),
        updatedBy: DefaultUser);

    // Trade Spread Data
    public static readonly OptionTradeSpreadsDataModel TradeSpreadData = new OptionTradeSpreadsDataModel(
        orderId: OrderId,
        tradeId: TradeId,
        valueDate: DefaultValueDate,
        tradeType: TradeType.LongIronCondor,
        sequenceId: 1,
        lossLimit: 12.5m,
        winLimit: 5.25m,
        forwardSpread: 8.75m,
        netSpread: 6.5m,
        createdOn: DefaultCreatedOn.AddMinutes(20),
        createdBy: DefaultUser);

    // Option Legs
    public static readonly List<OptionTradeLegReadModel> OptionLegs = new List<OptionTradeLegReadModel>
        {
            // Long Put leg
            new OptionTradeLegReadModel(
                orderId: OrderId,
                tradeId: TradeId,
                contractId: "ES202506P4700",
                quantity: 1,
                strikePrice: 4700m,
                optionLegType: OptionType.Put,
                optionLegAction: OptionLegAction.Long,
                createdOn: DefaultCreatedOn,
                createdBy: DefaultUser,
                updatedOn: DefaultCreatedOn,
                updatedBy: DefaultUser),

            // Short Put leg
            new OptionTradeLegReadModel(
                orderId: OrderId,
                tradeId: TradeId,
                contractId: "ES202506P4750",
                quantity: 1,
                strikePrice: 4750m,
                optionLegType: OptionType.Put,
                optionLegAction: OptionLegAction.Short,
                createdOn: DefaultCreatedOn,
                createdBy: DefaultUser,
                updatedOn: DefaultCreatedOn,
                updatedBy: DefaultUser),
            
            // Short Call leg
            new OptionTradeLegReadModel(
                orderId: OrderId,
                tradeId: TradeId,
                contractId: "ES202506C5000",
                quantity: 1,
                strikePrice: 5000m,
                optionLegType: OptionType.Call,
                optionLegAction: OptionLegAction.Short,
                createdOn: DefaultCreatedOn,
                createdBy: DefaultUser,
                updatedOn: DefaultCreatedOn,
                updatedBy: DefaultUser),

            // Long Call leg
            new OptionTradeLegReadModel(
                orderId: OrderId,
                tradeId: TradeId,
                contractId: "ES202506C5050",
                quantity: 1,
                strikePrice: 5050m,
                optionLegType: OptionType.Call,
                optionLegAction: OptionLegAction.Long,
                createdOn: DefaultCreatedOn,
                createdBy: DefaultUser,
                updatedOn: DefaultCreatedOn,
                updatedBy: DefaultUser)
        };

    // Trade Positions - primary focus based on the request
    public static readonly List<TradePositionReadModel> TradePositions = new List<TradePositionReadModel>
        {
            // Main position for LongIronCondor
            new TradePositionReadModel(
                orderId: OrderId,
                tradeId: TradeId,
                tradeType: TradeType.LongIronCondor,
                valueDate: DefaultValueDate,
                daysToExpiry: 95, // ~3 months to maturity
                tradeStatus: TradeStatus.Open,
                commission: 5.6m,
                deltaHedge: 0,
                netSpread: 8.5m,
                tradeValue: 850m,
                tradePnl: 0m, // New position, no P&L yet
                assetPrice: 4875.5m, // Current price of the underlying
                otmProbability: 0.68, // 68% probability out-of-the-money
                forwardPrice: 4885m,
                forwardLossRatio: 0.3, // 30% loss ratio
                lossProbability: 0.32, // 32% probability of loss
                riskFreeRate: 0.042, // 4.2% risk-free rate
                createdOn: DefaultCreatedOn,
                createdBy: DefaultUser,
                updatedOn: DefaultCreatedOn,
                updatedBy: DefaultUser),

            // Secondary position for PutSpread component
            new TradePositionReadModel(
                orderId: OrderId,
                tradeId: TradeId,
                tradeType: TradeType.LongIronCondor,
                valueDate: DefaultValueDate.AddDays(1),
                daysToExpiry: 95,
                tradeStatus: TradeStatus.IntraDay,
                commission: 2.8m,
                deltaHedge: 0,
                netSpread: 4.5m,
                tradeValue: 450m,
                tradePnl: 0m,
                assetPrice: 4875.5m,
                otmProbability: 0.72,
                forwardPrice: 4885m,
                forwardLossRatio: 0.25,
                lossProbability: 0.28,
                riskFreeRate: 0.042,
                createdOn: DefaultCreatedOn,
                createdBy: DefaultUser,
                updatedOn: DefaultCreatedOn,
                updatedBy: DefaultUser),

            // Secondary position for CallSpread component
            new TradePositionReadModel(
                orderId: OrderId,
                tradeId: TradeId,
                tradeType: TradeType.LongIronCondor,
                valueDate: DefaultValueDate.AddDays(2),
                daysToExpiry: 95,
                tradeStatus: TradeStatus.EndOfDay,
                commission: 2.8m,
                deltaHedge: 0,
                netSpread: 4m,
                tradeValue: 400m,
                tradePnl: 0m,
                assetPrice: 4875.5m,
                otmProbability: 0.75,
                forwardPrice: 4885m,
                forwardLossRatio: 0.22,
                lossProbability: 0.25,
                riskFreeRate: 0.042,
                createdOn: DefaultCreatedOn,
                createdBy: DefaultUser,
                updatedOn: DefaultCreatedOn,
                updatedBy: DefaultUser)
        };

    // Option Leg Data - for each leg in each position
    public static readonly List<OptionTradeLegDataReadModel> OptionLegsData = new List<OptionTradeLegDataReadModel>
        {
            // Long Put option leg data
            new OptionTradeLegDataReadModel(
                orderId: OrderId,
                tradeId: TradeId,
                valueDate: DefaultValueDate,
                tradeType: TradeType.LongIronCondor,
                daysToExpiry: 95,
                tradeStatus: TradeStatus.Open,
                optionLegId: "ES202506P4700",
                bidPrice: 25.5m,
                askPrice: 26.25m,
                impliedVolatility: 0.22,
                delta: -0.28,
                gamma: 0.015,
                theta: -0.14,
                vega: 0.32,
                rho: 0.11,
                createdOn: DefaultCreatedOn,
                createdBy: DefaultUser,
                updatedOn: DefaultCreatedOn,
                updatedBy: DefaultUser),

            // Short Put option leg data
            new OptionTradeLegDataReadModel(
                orderId: OrderId,
                tradeId: TradeId,
                valueDate: DefaultValueDate,
                tradeType: TradeType.LongIronCondor,
                daysToExpiry: 95,
                tradeStatus: TradeStatus.Open,
                optionLegId: "ES202506P4750",
                bidPrice: 34.25m,
                askPrice: 35m,
                impliedVolatility: 0.23,
                delta: -0.42,
                gamma: 0.02,
                theta: -0.17,
                vega: 0.36,
                rho: 0.14,
                createdOn: DefaultCreatedOn,
                createdBy: DefaultUser,
                updatedOn: DefaultCreatedOn,
                updatedBy: DefaultUser),

            // Short Call option leg data
            new OptionTradeLegDataReadModel(
                orderId: OrderId,
                tradeId: TradeId,
                valueDate: DefaultValueDate,
                tradeType: TradeType.LongIronCondor,
                daysToExpiry: 95,
                tradeStatus: TradeStatus.Open,
                optionLegId: "ES202506C5000",
                bidPrice: 29.5m,
                askPrice: 30.25m,
                impliedVolatility: 0.21,
                delta: 0.4,
                gamma: 0.018,
                theta: -0.16,
                vega: 0.33,
                rho: 0.13,
                createdOn: DefaultCreatedOn,
                createdBy: DefaultUser,
                updatedOn: DefaultCreatedOn,
                updatedBy: DefaultUser),

            // Long Call option leg data
            new OptionTradeLegDataReadModel(
                orderId: OrderId,
                tradeId: TradeId,
                valueDate: DefaultValueDate,
                tradeType: TradeType.LongIronCondor,
                daysToExpiry: 95,
                tradeStatus: TradeStatus.Open,
                optionLegId: "ES202506C5050",
                bidPrice: 21m,
                askPrice: 21.75m,
                impliedVolatility: 0.2,
                delta: 0.27,
                gamma: 0.014,
                theta: -0.12,
                vega: 0.3,
                rho: 0.1,
                createdOn: DefaultCreatedOn,
                createdBy: DefaultUser,
                updatedOn: DefaultCreatedOn,
                updatedBy: DefaultUser)
        };

    // Trade Limit
    public static readonly TradeLimitReadModel TradeLimit = new (
        tradeId: TradeId,
        tradeType: TradeType.LongIronCondor,
        riskMargin: 5000m, // $5000 risk margin
        maxProfit: 850m,   // Maximum profit potential
        maxLoss: 4150m,    // Maximum loss potential
        maxReturn: 0.17m,  // Max return ratio (17%)
        maxLossLimit: 12.5m,
        minProfitLimit: 5.5m,
        maxProfitLimit: 8.5m,
        minProfitTarget: 425m,  // Target profit
        dailyProfitTarget: 42.5m, // Daily profit target
        createdOn: DefaultCreatedOn,
        createdBy: DefaultUser,
        updatedOn: DefaultCreatedOn,
        updatedBy: DefaultUser);

    // Trade Type Limits
    public static readonly List<TradeTypeLimitReadModel> TradeTypeLimits = new List<TradeTypeLimitReadModel>
        {
            new (
                tradeId: TradeId,
                tradeType: TradeType.LongIronCondor,
                maxLossLimit: 12.5m,
                minProfitLimit: 5.5m,
                maxProfitLimit: 8.5m),

            new (
                tradeId: TradeId,
                tradeType: TradeType.ShortCall,
                maxLossLimit: 6.25m,
                minProfitLimit: 2.75m,
                maxProfitLimit: 4.5m),

            new (
                tradeId: TradeId,
                tradeType: TradeType.LongCall,
                maxLossLimit: 6.25m,
                minProfitLimit: 2.75m,
                maxProfitLimit: 4m)
        };

    // Trade Fills
    public static readonly List<TradeFillReadModel> TradeFills = [
            new (
                orderId: OrderId,
                tradeId: TradeId,
                fillDate: DefaultCreatedOn.AddMinutes(5),
                fillQuantity: 1,  // One iron condor contract
                createdOn: DefaultCreatedOn.AddMinutes(5),
                createdBy: DefaultUser)
        ];

    // Trade Fill Data
    public static readonly List<TradeFillDataReadModel> TradeFillData = [
            new (
                orderId: OrderId,
                tradeId: TradeId,
                contractId: "ES202506P4700",
                fillDate: DefaultCreatedOn.AddMinutes(5),
                bidPrice: 25.5m,
                askPrice: 26.25m,
                commission: 1.4m,
                optionLegAction: OptionLegAction.Long,
                createdOn: DefaultCreatedOn.AddMinutes(5),
                createdBy: DefaultUser),

            new (
                orderId: OrderId,
                tradeId: TradeId,
                contractId: "ES202506P4750",
                fillDate: DefaultCreatedOn.AddMinutes(5),
                bidPrice: 34.25m,
                askPrice: 35m,
                commission: 1.4m,
                optionLegAction: OptionLegAction.Short,
                createdOn: DefaultCreatedOn.AddMinutes(5),
                createdBy: DefaultUser),

            new (
                orderId: OrderId,
                tradeId: TradeId,
                contractId: "ES202506C5000",
                fillDate: DefaultCreatedOn.AddMinutes(5),
                bidPrice: 29.5m,
                askPrice: 30.25m,
                commission: 1.4m,
                optionLegAction: OptionLegAction.Short,
                createdOn: DefaultCreatedOn.AddMinutes(5),
                createdBy: DefaultUser),

            new (
                orderId: OrderId,
                tradeId: TradeId,
                contractId: "ES202506C5050",
                fillDate: DefaultCreatedOn.AddMinutes(5),
                bidPrice: 21m,
                askPrice: 21.75m,
                commission: 1.4m,
                optionLegAction: OptionLegAction.Long,
                createdOn: DefaultCreatedOn.AddMinutes(5),
                createdBy: DefaultUser)
        ];

    public static readonly TradePlanReadModel TradePlan = new TradePlanReadModel(
            sequenceId: 1,
            orderId: 1001,
            tradeId: 2001,
            valueDate: new DateOnly(2025, 3, 15),
            actionDate: new DateTime(2025, 3, 15, 9, 30, 0),
            tradeDate: new DateOnly(2025, 3, 15),
            maturityDate: new DateOnly(2025, 6, 18),
            tradeType: TradeType.LongIronCondor,
            actionType: ActionType.OpenTradePosition,
            actionSubType: ActionSubType.TradeInProfitPosition,
            actionState: ActionState.Normal,
            actionReason: "Initial trade setup",
            tradePnl: 0m,
            forwardLossRatio: 0.3,
            lossProbability: 0.32,
            mScore: 0.75,
            maxProfit: 850m,
            maxLoss: 4150m,
            minProfitTarget: 425m,
            dailyProfitTarget: 42.5m,
            assetPrice: 4875.5m,
            assetStdDev: 0.22,
            assetMean: 0.15,
            assetPriceChange: 0.05,
            marketTrend: MarketDirectionType.Up,
            marketVolatility: MarketVolatilityType.High,
            marketDirection: PriceDirectionType.RisingSlowly,
            vixVolatility: PriceVolatilityType.Flat,
            tradeRisk: TradeRiskType.High,
            fiftyDayMA: 4850.5,
            fiveDayXMA: 4870.5,
            putOTMProbability: 0.68,
            callOTMProbability: 0.72,
            shortPutGamma: 0.015,
            shortCallGamma: 0.018,
            gammaRisk: GammaRiskType.LowShortPutGamma,
            netPrice: 8.5m,
            forwardPrice: 4885m,
            forwardDelta: 0.25,
            stopLossLimit: 12.5,
            trendType: FuturesTrendType.UpTrend,
            trendStrength: FuturesTrendStrengthType.High,
            rsi: 55.5,
            rsiSlope: 0.5,
            tdi: FuturesTrendDirectionType.UpTrending,
            tdiStrength: FuturesTrendDirectionStrengthType.Medium,
            createdOn: new DateTime(2025, 3, 15, 9, 30, 0),
            createdBy: "testuser"
        );

    /// <summary>
    /// Sample TradeOrder for testing
    /// </summary>
    public static TradeOrderReadModel TradeOrder => new TradeOrderReadModel(
        fundId: 1001,
        orderId: OrderId,
        tradeId: TradeId,
        valueDate: new DateOnly(2025, 3, 15),
        tradeType: TradeType.ShortIronCondor,
        tradeSubType: TradeSubType.Primary,
        tradeDate: new DateOnly(2025, 3, 15),
        maturityDate: new DateOnly(2025, 4, 17),
        tradeOrderState: TradeOrderState.OrderFilled,
        underlyingContractId: "SPY",
        underlyingAssetType: AssetType.Futures,
        orderDescription: "SPY Iron Condor 410/415-400/395",
        orderAction: OrderAction.Buy,
        orderActionType: OrderActionType.Open,
        orderQuantity: 10,
        orderFilled: 10,
        orderType: OrderType.Limit,
        orderPrice: 2.50m,
        orderAmount: 2500.00m,
        commission: 32.80m,
        totalAmount: 2532.80m,
        tradePnl: 0.0m,
        tradeFillType: TradeFillType.Broker,
        createdOn: DateTime.Parse("2025-03-15T09:30:00Z"),
        createdBy: "UnitTest",
        updatedOn: DateTime.Parse("2025-03-15T09:45:00Z"),
        updatedBy: "UnitTest")
    {
    };

}
