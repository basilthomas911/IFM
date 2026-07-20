using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.TradeOrder;
using TomasAI.IFM.Shared.TradeOrder.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Actor.UnitTests;

public static class SampleData
{
    public static readonly OptionTradeEntityId EntityId1 = new(100, 1);
    public static readonly OptionTradeEntityId EntityId2 = new(200, 2);

    public static TradePlanReadModel CreateTradePlan(
        int orderId = 100,
        int tradeId = 1,
        decimal assetPrice = 5800.00m,
        TradeType tradeType = TradeType.ShortIronCondor,
        ActionType actionType = ActionType.HoldTradePosition,
        ActionState actionState = ActionState.Normal)
        => new TradePlanReadModel(
            sequenceId: 1L,
            orderId: orderId,
            tradeId: tradeId,
            valueDate: new DateOnly(2025, 1, 15),
            actionDate: DateTime.UtcNow,
            tradeDate: new DateOnly(2025, 1, 15),
            maturityDate: new DateOnly(2025, 3, 21),
            tradeType: tradeType,
            actionType: actionType,
            actionSubType: ActionSubType.None,
            actionState: actionState,
            actionReason: "Test plan",
            tradePnl: 0m,
            forwardLossRatio: 0.5,
            lossProbability: 0.2,
            mScore: 1.5,
            maxProfit: 250.00m,
            maxLoss: 750.00m,
            minProfitTarget: 50.00m,
            dailyProfitTarget: 25.00m,
            assetPrice: assetPrice,
            assetStdDev: 12.5,
            assetMean: 5790.0,
            assetPriceChange: 0.18,
            marketTrend: MarketDirectionType.Up,
            marketVolatility: MarketVolatilityType.Normal,
            marketDirection: PriceDirectionType.Rising,
            vixVolatility: PriceVolatilityType.Falling,
            tradeRisk: TradeRiskType.Low,
            fiftyDayMA: 5700.0,
            fiveDayXMA: 5780.0,
            putOTMProbability: 0.85,
            callOTMProbability: 0.80,
            shortPutGamma: 0.02,
            shortCallGamma: 0.02,
            gammaRisk: GammaRiskType.None,
            netPrice: 2.50m,
            forwardPrice: 5810.00m,
            forwardDelta: 0.05,
            stopLossLimit: 0.6,
            trendType: FuturesTrendType.UpTrend,
            trendStrength: FuturesTrendStrengthType.High,
            rsi: 55.0,
            rsiSlope: 0.5,
            tdi: FuturesTrendDirectionType.UpTrending,
            tdiStrength: FuturesTrendDirectionStrengthType.High,
            createdOn: DateTime.UtcNow,
            createdBy: "TestUser");

    public static TradeOrderReadModel CreateTradeOrder(
        int orderId = 100,
        int tradeId = 1,
        TradeType tradeType = TradeType.ShortIronCondor,
        TradeFillType tradeFillType = TradeFillType.Manual,
        OrderActionType orderActionType = OrderActionType.Open)
        => new TradeOrderReadModel(
            fundId: 1,
            orderId: orderId,
            tradeId: tradeId,
            valueDate: new DateOnly(2025, 1, 15),
            tradeType: tradeType,
            tradeSubType: TradeSubType.Primary,
            tradeDate: new DateOnly(2025, 1, 15),
            maturityDate: new DateOnly(2025, 3, 21),
            tradeOrderState: TradeOrderState.OrderPlaced,
            underlyingContractId: "ES-MAR25",
            underlyingAssetType: AssetType.Futures,
            orderDescription: "Test Iron Condor",
            orderAction: OrderAction.Buy,
            orderActionType: orderActionType,
            orderQuantity: 1,
            orderFilled: 0,
            orderType: OrderType.Limit,
            orderPrice: 2.50m,
            orderAmount: 250.00m,
            commission: 5.00m,
            totalAmount: 255.00m,
            tradePnl: 0m,
            tradeFillType: tradeFillType,
            createdOn: DateTime.UtcNow,
            createdBy: "TestUser",
            updatedOn: DateTime.UtcNow,
            updatedBy: "TestUser")
        .SetTradeLimit(new TradeLimitReadModel { TradeId = tradeId, TradeType = tradeType });

    public static OptionTradeReadModel CreateOptionTrade(
        int orderId = 100,
        int tradeId = 1,
        TradeType tradeType = TradeType.ShortIronCondor,
        TradeState tradeState = TradeState.TradeToOpen)
        => new(
            orderId: orderId,
            tradeId: tradeId,
            tradeStrategy: "IronCondor",
            tradeDate: new DateOnly(2025, 1, 15),
            maturityDate: new DateOnly(2025, 3, 21),
            tradeType: tradeType,
            tradeState: tradeState,
            tradeAction: TradeAction.Buy,
            underlyingContractId: "ES-MAR25",
            underlyingAssetType: AssetType.Futures,
            isPrimaryTrade: true,
            isHedgeTrade: false,
            createdOn: DateTime.UtcNow,
            createdBy: "TestUser",
            updatedOn: DateTime.UtcNow,
            updatedBy: "TestUser");
}
