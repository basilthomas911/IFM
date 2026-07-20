using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.TradeOrder;
using TomasAI.IFM.Shared.TradeOrder.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Actor.IntegratedTests;

public static class SampleData
{
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

    public static TradePlanReadModel CreateTradePlan(
        int orderId = 300,
        int tradeId = 1,
        DateOnly valueDate = default,
        TradeType tradeType = TradeType.ShortIronCondor)
    {
        var date = valueDate == default ? new DateOnly(2025, 1, 15) : valueDate;
        return new TradePlanReadModel
        {
            OrderId = orderId,
            TradeId = tradeId,
            ValueDate = date,
            TradeDate = date,
            MaturityDate = new DateOnly(2025, 3, 21),
            TradeType = tradeType,
            ActionType = ActionType.HoldTradePosition,
            ActionSubType = ActionSubType.None,
            ActionState = ActionState.Normal,
            ActionReason = "Test",
            ForwardLossRatio = 0.25,
            LossProbability = 0.10,
            MScore = 1.5,
            MaxProfit = 500m,
            MaxLoss = -1000m,
            StopLossLimit = 0.50,
            CreatedOn = DateTime.UtcNow,
            CreatedBy = "TestUser"
        };
    }

    public static TradePlanForwardLossLimitReadModel CreateTradePlanForwardLossLimit(
        int orderId = 300,
        int tradeId = 1,
        DateOnly valueDate = default,
        TradeType tradeType = TradeType.ShortIronCondor,
        ForwardLossLimitType limitType = ForwardLossLimitType.LimitWarning)
    {
        var date = valueDate == default ? new DateOnly(2025, 1, 15) : valueDate;
        return new TradePlanForwardLossLimitReadModel(orderId, tradeId, date, tradeType, limitType);
    }

    public static TradePlanForwardLossRatioReadModel CreateTradePlanForwardLossRatio(
        double forwardLossRatio = 0.25)
        => new TradePlanForwardLossRatioReadModel(forwardLossRatio);
}
