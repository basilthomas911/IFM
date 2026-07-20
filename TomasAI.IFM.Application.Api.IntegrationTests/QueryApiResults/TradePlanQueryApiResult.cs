using Microsoft.AspNetCore.Http;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketDataAnalytics;

namespace TomasAI.IFM.Application.Api.IntegrationTests.QueryApiResults;

public static class TradePlanQueryApiResult
{
    public static Task FromGetLossProbabilityAsync(HttpResponse resp)
        => resp.SetResult(new LossProbabilityDataModel(Value: 0.05, Threshold: 0m, ThresholdCount: 0));

    public static Task FromGetLossProbabilityDistributionAsync(HttpResponse resp)
        => resp.SetResult(new LossProbabilityDistributionDataModel(Mean: 0.8, StdDev: 0.1));

    public static Task FromGetStopLossLimitAsync(HttpResponse resp)
        => resp.SetResult(new TradePlanStopLossLimitReadModel(stopLossLimit: 0.15));

    public static Task FromGetTradePlanForwardLossRatiosAsync(HttpResponse resp)
        => resp.SetResult(new TradePlanForwardLossRatioReadModel[] { new(0.75), new(0.82), new(0.68) });

    public static Task FromGetTradePlanForwardLossRatioAsync(HttpResponse resp)
        => resp.SetResult(new TradePlanForwardLossRatioReadModel(0.80));

    public static Task FromGetTradePlanActionAsync(HttpResponse resp)
        => resp.SetResult(new TradePlanActionReadModel[]
        {
            new(
                TradePlanId: System.Guid.NewGuid(),
                OrderId: 1,
                TradeId: 1,
                ValueDate: DateOnly.FromDateTime(System.DateTime.UtcNow.Date),
                ActionDate: System.DateTime.UtcNow,
                ActionType: ActionType.HoldTradePosition,
                ActionSubType: ActionSubType.None,
                ActionState: ActionState.Normal,
                ActionReason: "UnitTest",
                MarketTrend: MarketDirectionType.NeutralUp,
                MarketVolatility: MarketVolatilityType.Normal,
                MarketDirection: PriceDirectionType.Rising,
                VixVolatility: PriceVolatilityType.Rising,
                TradeRisk: TradeRiskType.Low,
                GammaRisk: GammaRiskType.None,
                TradePnl: 0m,
                ForwardLossRatio: 0.8,
                MScore: 0.75,
                NetPrice: 0m,
                ForwardPrice: 0m,
                StopLossLimit: 0.15,
                CreatedOn: System.DateTime.UtcNow,
                CreatedBy: "UnitTest"
            )
        });

    public static Task FromGetTradePlansAsync(HttpResponse resp)
        => resp.SetResult(new TradePlanReadModel[]
        {
            new(
                sequenceId: 0,
                orderId: 1,
                tradeId: 1,
                valueDate: DateOnly.FromDateTime(System.DateTime.UtcNow.Date),
                actionDate: System.DateTime.UtcNow,
                tradeDate: DateOnly.FromDateTime(System.DateTime.UtcNow.Date),
                maturityDate: DateOnly.FromDateTime(System.DateTime.UtcNow.Date.AddDays(30)),
                tradeType: TradeType.ShortIronCondor,
                actionType: ActionType.HoldTradePosition,
                actionSubType: ActionSubType.None,
                actionState: ActionState.Normal,
                actionReason: "UnitTest",
                tradePnl: 0m,
                forwardLossRatio: 0.8,
                lossProbability: 0.05,
                mScore: 0.75,
                maxProfit: 1000m,
                maxLoss: 500m,
                minProfitTarget: 50m,
                dailyProfitTarget: 10m,
                assetPrice: 100m,
                assetStdDev: 1.0,
                assetMean: 100.0,
                assetPriceChange: 0.01,
                marketTrend: MarketDirectionType.NeutralUp,
                marketVolatility: MarketVolatilityType.Normal,
                marketDirection: PriceDirectionType.Rising,
                vixVolatility: PriceVolatilityType.Rising,
                tradeRisk: TradeRiskType.Low,
                fiftyDayMA: 101.0,
                fiveDayXMA: 100.5,
                putOTMProbability: 0.2,
                callOTMProbability: 0.2,
                shortPutGamma: 0.01,
                shortCallGamma: 0.01,
                gammaRisk: GammaRiskType.None,
                netPrice: 0m,
                forwardPrice: 0m,
                forwardDelta: 0.0,
                stopLossLimit: 0.15,
                trendType: FuturesTrendType.RangeBound,
                trendStrength: FuturesTrendStrengthType.None,
                rsi: 50.0,
                rsiSlope: 0.0,
                tdi: FuturesTrendDirectionType.Init,
                tdiStrength: FuturesTrendDirectionStrengthType.Low,
                createdOn: System.DateTime.UtcNow,
                createdBy: "UnitTest"
            )
        });

    public static Task FromGetIronCondorForwardDeltaAsync(HttpResponse resp)
        => resp.SetResult(new IronCondorForwardDeltaDataModel(0.123));

    public static Task FromGetTradePlanForwardLossLimitAsync(HttpResponse resp)
        => resp.SetResult(new TradePlanForwardLossLimitReadModel(
            orderId: 1,
            tradeId: 1,
            valueDate: DateOnly.FromDateTime(System.DateTime.UtcNow.Date),
            tradeType: TradeType.ShortIronCondor,
            limitType: ForwardLossLimitType.LimitWarning
        ));
}
