using Microsoft.AspNetCore.Http;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.MarketData;

namespace TomasAI.IFM.Application.Api.IntegrationTests.QueryApiResults;

public static class OptionTradeQueryApiResult
{
    public static Task FromGetTradeHistoryAsync(HttpResponse resp)
        => resp.SetResult(new[] {
            new TradeHistoryReadModel(
                orderId: 1,
                tradeId: 1,
                tradeType: TradeType.ShortIronCondor,
                valueDate: new System.DateOnly(2025, 10, 10),
                daysToExpiry: 30,
                tradeStatus: TradeStatus.Open,
                commission: 0m,
                netSpread: 100m,
                tradePnl: 50m)
        });

    public static Task FromGetOptionLegContractIdsAsync(HttpResponse resp)
        => resp.SetResult(new[] { "ES20251010C4500", "ES20251010P4450" });

    public static Task FromGetTradeLimitAsync(HttpResponse resp)
        => resp.SetResult(TradeLimitReadModel.Default(1, TradeType.ShortIronCondor));

    public static Task FromGetTradeTypeLimitAsync(HttpResponse resp)
        => resp.SetResult(new TradeTypeLimitReadModel(1, TradeType.ShortIronCondor, 1000m, 100m, 2000m));

    public static Task FromGetTradeQuantityAsync(HttpResponse resp)
        => resp.SetResult(new ScalarReadModel<int>(10));

    public static Task FromGetOptionTradeAsync(HttpResponse resp)
        => resp.SetResult(new OptionTradeReadModel(
            orderId: 1,
            tradeId: 1,
            tradeStrategy: "IC",
            tradeDate: new System.DateOnly(2025, 10, 10),
            maturityDate: new System.DateOnly(2025, 10, 10),
            tradeType: TradeType.ShortIronCondor,
            tradeState: TradeState.NewTrade,
            tradeAction: TradeAction.Buy,
            underlyingContractId: "ES20251010",
            underlyingAssetType: AssetType.Futures,
            isPrimaryTrade: true,
            isHedgeTrade: false,
            createdOn: System.DateTime.UtcNow,
            createdBy: "tester",
            updatedOn: System.DateTime.UtcNow,
            updatedBy: "tester"));

    public static Task FromGetOptionTradeSpreadDataAsync(HttpResponse resp)
        => resp.SetResult(new OptionTradeSpreadsDataModel(
            orderId: 1,
            tradeId: 1,
            valueDate: new System.DateOnly(2025, 10, 10),
            tradeType: TradeType.ShortIronCondor,
            sequenceId: 1,
            lossLimit: 10m,
            winLimit: 20m,
            forwardSpread: 5m,
            netSpread: 15m,
            createdOn: System.DateTime.UtcNow,
            createdBy: "tester"));

    public static Task FromGetOptionTradeSpreadBarDataAsync(HttpResponse resp)
        => resp.SetResult(new[] {
            new OptionTradeSpreadBarsDataModel(1, 1, new System.DateOnly(2025,10,10), TradeType.ShortIronCondor, System.DateTime.UtcNow, 10m, 20m, 5m, 15m),
            new OptionTradeSpreadBarsDataModel(1, 1, new System.DateOnly(2025,10,10), TradeType.ShortIronCondor, System.DateTime.UtcNow.AddMinutes(-1), 11m, 21m, 5.1m, 15.1m)
        });

    public static Task FromGetOptionTradesAsync(HttpResponse resp)
        => resp.SetResult(new[] {
            new OptionTradeReadModel(1, 1, "IC", new System.DateOnly(2025,10,10), new System.DateOnly(2025,10,10), TradeType.ShortIronCondor, TradeState.NewTrade, TradeAction.Buy, "ES20251010", AssetType.Futures, true, false, System.DateTime.UtcNow, "tester", System.DateTime.UtcNow, "tester")
        });

    public static Task FromGetTradePositionsAsync(HttpResponse resp)
        => resp.SetResult(new[] {
            TradePositionReadModel.Default(orderId: 1, tradeId: 1, tradeType: TradeType.ShortIronCondor, valueDate: new System.DateOnly(2025,10,10), daysToExpiry: 30, tradeStatus: TradeStatus.Open)
        });

    public static Task FromGetTradePositionAsync(HttpResponse resp)
        => resp.SetResult(TradePositionReadModel.Default(orderId: 1, tradeId: 1, tradeType: TradeType.ShortIronCondor, valueDate: new System.DateOnly(2025,10,10), daysToExpiry: 30, tradeStatus: TradeStatus.Open));

    public static Task FromGetIronCondorTradePriceAsync(HttpResponse resp)
        => resp.SetResult(new TradePriceReadModel(1, new System.DateOnly(2025,10,10), 4500m, 4501m));

    public static Task FromGetTradePlanSummaryAsync(HttpResponse resp)
        => resp.SetResult(new[] {
            new TradePlanActionReadModel(
                TradePlanId: System.Guid.NewGuid(),
                OrderId: 1,
                TradeId: 1,
                ValueDate: new System.DateOnly(2025,10,10),
                ActionDate: System.DateTime.UtcNow,
                ActionType: ActionType.HoldTradePosition,
                ActionSubType: ActionSubType.None,
                ActionState: ActionState.Normal,
                ActionReason: "sample",
                MarketTrend: MarketDirectionType.Up,
                MarketVolatility: MarketVolatilityType.Rising,
                MarketDirection: PriceDirectionType.Rising,
                VixVolatility: PriceVolatilityType.Rising,
                TradeRisk: TradeRiskType.Low,
                GammaRisk: GammaRiskType.LowShortPutGamma,
                TradePnl: 0m,
                ForwardLossRatio: 0.0,
                MScore: 0.0,
                NetPrice: 4500m,
                ForwardPrice: 4501m,
                StopLossLimit: 0.1,
                CreatedOn: System.DateTime.UtcNow,
                CreatedBy: "tester")
        });

    public static Task FromGetTradePositionTradeTypesAsync(HttpResponse resp)
        => resp.SetResult(new[] { "Put", "Call" });

    public static Task FromGetIronCondorMDILimitAsync(HttpResponse resp)
        => resp.SetResult(new IronCondorMDILimitDataModel(new OptionTradeEntityId(1, 1), new System.DateOnly(2025,10,10), 0.5, 0.4, 0.7));
}
