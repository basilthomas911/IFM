using Shared = TomasAI.IFM.Shared;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.TradeOrder;
using TomasAI.IFM.Shared.TradeOrder.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Actor.BDDTests;

public static class SampleData
{
    static readonly DateTime CreatedOn = new(2024, 6, 15);
    static readonly DateOnly TradeDate = new(2024, 6, 15);
    static readonly DateOnly MaturityDate = new(2024, 7, 19);

    public static OptionTradeReadModel OptionTrade => new(
        orderId: 1234,
        tradeId: 4567,
        tradeStrategy: "ShortIronCondor",
        tradeDate: TradeDate,
        maturityDate: MaturityDate,
        tradeType: TradeType.ShortIronCondor,
        tradeState: TradeState.TradeToOpen,
        tradeAction: TradeAction.Sell,
        underlyingContractId: "ES20240719",
        underlyingAssetType: AssetType.Futures,
        isPrimaryTrade: true,
        isHedgeTrade: false,
        createdOn: CreatedOn,
        createdBy: "basilt",
        updatedOn: CreatedOn,
        updatedBy: "basilt"
    );

    public static OptionTradeEntityId OptionTradeId => new(OptionTrade.OrderId, OptionTrade.TradeId);

    public static OptionTradeEntityId NonExistingOptionTradeId => new(9999, 8888);

    public static OptionTradeSpreadsDataModel OptionTradeSpreadData => new(
        orderId: 1234,
        tradeId: 4567,
        valueDate: TradeDate,
        tradeType: TradeType.PutCreditSpread,
        sequenceId: 100L,
        lossLimit: -250m,
        winLimit: 250m,
        forwardSpread: 2.50m,
        netSpread: 2.25m,
        createdOn: CreatedOn,
        createdBy: "basilt"
    );

    public static OptionTradeSpreadBarsDataModel OptionTradeSpreadBarData => new(
        orderId: 1234,
        tradeId: 4567,
        valueDate: TradeDate,
        tradeType: TradeType.PutCreditSpread,
        barDate: CreatedOn,
        lossLimit: -250m,
        winLimit: 250m,
        forwardSpread: 2.50m,
        netSpread: 2.25m
    );

    public static TradePriceReadModel TradePrice => new(
        tradeId: 4567,
        valueDate: TradeDate,
        netPrice: 2.50m,
        netForwardPrice: 2.75m
    );

    public static OptionTradeLegReadModel[] OptionTradeLegs =>
    [
        new(orderId: 1234, tradeId: 4567, contractId: "ES20240719P5300", quantity: -1, strikePrice: 5300m, optionLegType: OptionType.Put, optionLegAction: OptionLegAction.Short, createdOn: CreatedOn, createdBy: "basilt", updatedOn: CreatedOn, updatedBy: "basilt"),
        new(orderId: 1234, tradeId: 4567, contractId: "ES20240719P5250", quantity: 1, strikePrice: 5250m, optionLegType: OptionType.Put, optionLegAction: OptionLegAction.Long, createdOn: CreatedOn, createdBy: "basilt", updatedOn: CreatedOn, updatedBy: "basilt"),
        new(orderId: 1234, tradeId: 4567, contractId: "ES20240719C5500", quantity: -1, strikePrice: 5500m, optionLegType: OptionType.Call, optionLegAction: OptionLegAction.Short, createdOn: CreatedOn, createdBy: "basilt", updatedOn: CreatedOn, updatedBy: "basilt"),
        new(orderId: 1234, tradeId: 4567, contractId: "ES20240719C5550", quantity: 1, strikePrice: 5550m, optionLegType: OptionType.Call, optionLegAction: OptionLegAction.Long, createdOn: CreatedOn, createdBy: "basilt", updatedOn: CreatedOn, updatedBy: "basilt")
    ];

    public static string[] OptionLegContractIds => ["ES20240719P5300", "ES20240719P5250", "ES20240719C5500", "ES20240719C5550"];

    public static TradeOrderReadModel TradeOrder => new TradeOrderReadModel(
        fundId: 100,
        orderId: 1234,
        tradeId: 4567,
        valueDate: TradeDate,
        tradeType: TradeType.ShortIronCondor,
        tradeSubType: TradeSubType.Primary,
        tradeDate: TradeDate,
        maturityDate: MaturityDate,
        tradeOrderState: TradeOrderState.OrderPlaced,
        underlyingContractId: "ES20240719",
        underlyingAssetType: AssetType.Futures,
        orderDescription: "Short Iron Condor ES",
        orderAction: OrderAction.Sell,
        orderActionType: OrderActionType.Open,
        orderQuantity: 1,
        orderFilled: 0,
        orderType: OrderType.Limit,
        orderPrice: 2.50m,
        orderAmount: 250.0m,
        commission: 5.0m,
        totalAmount: 245.0m,
        tradePnl: 0.0m,
        tradeFillType: TradeFillType.Manual,
        createdOn: CreatedOn,
        createdBy: "basilt",
        updatedOn: CreatedOn,
        updatedBy: "basilt"
    )
    .AddOptionLegs([
        new OptionTradeLegReadModel(orderId: 1234, tradeId: 4567, contractId: "ES20240719P5300", quantity: -1, strikePrice: 5300m, optionLegType: OptionType.Put, optionLegAction: OptionLegAction.Short, createdOn: CreatedOn, createdBy: "basilt", updatedOn: CreatedOn, updatedBy: "basilt"),
        new OptionTradeLegReadModel(orderId: 1234, tradeId: 4567, contractId: "ES20240719P5250", quantity: 1, strikePrice: 5250m, optionLegType: OptionType.Put, optionLegAction: OptionLegAction.Long, createdOn: CreatedOn, createdBy: "basilt", updatedOn: CreatedOn, updatedBy: "basilt"),
        new OptionTradeLegReadModel(orderId: 1234, tradeId: 4567, contractId: "ES20240719C5500", quantity: -1, strikePrice: 5500m, optionLegType: OptionType.Call, optionLegAction: OptionLegAction.Short, createdOn: CreatedOn, createdBy: "basilt", updatedOn: CreatedOn, updatedBy: "basilt"),
        new OptionTradeLegReadModel(orderId: 1234, tradeId: 4567, contractId: "ES20240719C5550", quantity: 1, strikePrice: 5550m, optionLegType: OptionType.Call, optionLegAction: OptionLegAction.Long, createdOn: CreatedOn, createdBy: "basilt", updatedOn: CreatedOn, updatedBy: "basilt")
    ])
    .SetTradeLimit(new TradeLimitReadModel(
        tradeId: 4567,
        tradeType: TradeType.ShortIronCondor,
        riskMargin: 5000m,
        maxProfit: 250m,
        maxLoss: -250m,
        maxReturn: 0.05m,
        maxLossLimit: 10.0m,
        minProfitLimit: 1.0m,
        maxProfitLimit: 5.0m,
        minProfitTarget: 125m,
        dailyProfitTarget: 25m,
        createdOn: CreatedOn,
        createdBy: "basilt",
        updatedOn: CreatedOn,
        updatedBy: "basilt"
    ))
    .AddTradeTypeLimits([
        new TradeTypeLimitReadModel(tradeId: 4567, tradeType: TradeType.PutCreditSpread, maxLossLimit: 5.0m, minProfitLimit: 0.5m, maxProfitLimit: 2.5m),
        new TradeTypeLimitReadModel(tradeId: 4567, tradeType: TradeType.CallCreditSpread, maxLossLimit: 5.0m, minProfitLimit: 0.5m, maxProfitLimit: 2.5m)
    ]);

    public static TradeOrderReadModel TradeOrderForBroker => TradeOrder with
    {
        TradeFillType = TradeFillType.Broker
    };

    public static TradeOrderReadModel TradeOrderForClose => TradeOrder with
    {
        OrderActionType = OrderActionType.Close
    };

    public static OptionTradeReadModel OptionTradeOrderPlaced => OptionTrade with
    {
        TradeState = TradeState.OrderPlaced
    };

    public static OptionTradeReadModel OptionTradeOrderFilled => OptionTrade with
    {
        TradeState = TradeState.OrderFilled
    };

    public static OptionTradeReadModel OptionTradeInvalidBrokerState => OptionTrade with
    {
        TradeState = TradeState.NewTrade
    };

    public static OptionTradeReadModel OptionTradeToClose => OptionTrade with
    {
        TradeState = TradeState.TradeToClose
    };

    public static TradePlanReadModel TradePlan => new(
        sequenceId: 1L,
        orderId: 1234,
        tradeId: 4567,
        valueDate: TradeDate,
        actionDate: CreatedOn,
        tradeDate: TradeDate,
        maturityDate: MaturityDate,
        tradeType: TradeType.ShortIronCondor,
        actionType: Shared.Trade.ActionType.HoldTradePosition,
        actionSubType: Shared.Trade.ActionSubType.None,
        actionState: Shared.Trade.ActionState.Normal,
        actionReason: "Market update",
        tradePnl: 0m,
        forwardLossRatio: 0.15,
        lossProbability: 0.25,
        mScore: 0.60,
        maxProfit: 250m,
        maxLoss: -250m,
        minProfitTarget: 125m,
        dailyProfitTarget: 25m,
        assetPrice: 5400m,
        assetStdDev: 12.5,
        assetMean: 5390.0,
        assetPriceChange: 0.05,
        marketTrend: Shared.MarketData.MarketDirectionType.Up,
        marketVolatility: Shared.MarketData.MarketVolatilityType.Low,
        marketDirection: Shared.MarketData.PriceDirectionType.Rising,
        vixVolatility: Shared.MarketData.PriceVolatilityType.Falling,
        tradeRisk: Shared.Trade.TradeRiskType.Low,
        fiftyDayMA: 5350.0,
        fiveDayXMA: 5380.0,
        putOTMProbability: 0.85,
        callOTMProbability: 0.82,
        shortPutGamma: 0.03,
        shortCallGamma: 0.03,
        gammaRisk: Shared.Trade.GammaRiskType.None,
        netPrice: 2.50m,
        forwardPrice: 2.75m,
        forwardDelta: 0.026,
        stopLossLimit: 0.10,
        trendType: Shared.MarketDataAnalytics.FuturesTrendType.UpTrend,
        trendStrength: Shared.MarketDataAnalytics.FuturesTrendStrengthType.Low,
        rsi: 55.0,
        rsiSlope: 1.5,
        tdi: Shared.MarketDataAnalytics.FuturesTrendDirectionType.UpTrending,
        tdiStrength: Shared.MarketDataAnalytics.FuturesTrendDirectionStrengthType.Low,
        createdOn: CreatedOn,
        createdBy: "basilt"
    );

    public static TradePlanReadModel TradePlanWithChangedAssetPrice => TradePlan with
    {
        AssetPrice = 5450m
    };
}
