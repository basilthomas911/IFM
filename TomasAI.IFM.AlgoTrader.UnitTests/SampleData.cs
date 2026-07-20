using System;
using System.Collections.Generic;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketDataAnalytics;

namespace TomasAI.IFM.Service.AlgoTrader.UnitTests
{
    public static class SampleData
    {
        public static TradePlanReadModel TradePlan => new TradePlanReadModel
        (
            sequenceId: 0,
            orderId: 1124,
            tradeId: 1117,
            tradeType: TradeType.ShortIronCondor,
            tradeDate: new DateOnly(2019, 08, 15),
            valueDate: new DateOnly(2019, 08, 15),
            maturityDate: new DateOnly(2019, 08, 23),
            actionDate: new DateTime(2019, 08, 15, 11, 57, 11, 77),
            actionType: ActionType.HoldTradePosition,
            actionSubType: ActionSubType.TradeInProfitPosition,
            actionState: ActionState.Normal,
            actionReason: "Market=>Down",
            tradePnl: 185.50m,
            forwardLossRatio: 0.21,
            lossProbability: 0.1556439,
            mScore: 0.73,
            maxProfit: 1314.25m,
            maxLoss: -1314.25m,
            minProfitTarget: 736.645m,
            dailyProfitTarget: 267.27m,
            assetPrice: 2846.75m,
            assetStdDev: 67.81488,
            assetMean: 2940.419,
            assetPriceChange: 0.45,
            marketTrend: MarketDirectionType.Down,
            marketVolatility: MarketVolatilityType.Normal,
            marketDirection: PriceDirectionType.FallingSlowly,
            vixVolatility: PriceVolatilityType.Falling,
            tradeRisk: TradePlanReadModel.FromMScore(0.73),
            fiftyDayMA: 45.5,
            fiveDayXMA: 100.45,
            putOTMProbability: 0.8995341,
            callOTMProbability: 0.7498,
            shortPutGamma: 0.0005,
            shortCallGamma: 0.0004,
            gammaRisk: GammaRiskType.HighShortPutGamma,
            netPrice: 2.175m,
            forwardPrice: 2.2439m,
            forwardDelta: 0.50,
            stopLossLimit: 0.25,
            trendType: FuturesTrendType.UpTrend,
            trendStrength: FuturesTrendStrengthType.Medium,
            rsi: 45.56,
            rsiSlope: -0.4567,
            tdi: FuturesTrendDirectionType.TrendReversal,
            tdiStrength: FuturesTrendDirectionStrengthType.Low,
            createdOn: new DateTime(2019, 8, 15, 11, 57, 11, 077),
            createdBy: "basil"
        );
    }
}
