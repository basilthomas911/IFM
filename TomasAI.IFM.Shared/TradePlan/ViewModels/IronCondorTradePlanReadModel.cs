using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text;
using MessagePack;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Shared.TradePlan.ViewModels
{
    [MessagePackObject(true)]
    public record IronCondorTradePlanReadModel(
            Guid TradePlanId,
            int OrderId,
            int TradeId,
            TradeType TradeType,
            DateTime TradeDate,
            DateOnly valueDate,
            DateTime MaturityDate,
            DateTime ActionDate,
            ActionType ActionType,
            ActionSubType ActionSubType,
            ActionState ActionState,
            string ActionReason,
            decimal TradePnl,
            double ForwardLossRatio,
            double LossProbability,
            double MScore,
            decimal MaxProfit,
            decimal MaxLoss,
            decimal MinProfitTarget,
            decimal DailyProfitTarget,
            decimal AssetPrice,
            double AssetStdDev,
            double AssetMean,
            double AssetPriceChange,
            MarketDirectionType MarketTrend,
            MarketVolatilityType MarketVolatility,
            PriceDirectionType MarketDirection,
            PriceVolatilityType VixVolatility,
            TradeRiskType TradeRisk,
            double FiftyDayMA,
            double FiveDayXMA,
            double PutOTMProbability,
            double CallOTMProbability,
            double ShortPutGamma,
            double ShortCallGamma,
            GammaRiskType GammaRisk,
            decimal NetPrice,
            decimal ForwardPrice,
            double StopLossLimit,
            DateTime CreatedOn,
            string CreatedBy)
    {

        public static TradeRiskType FromMScore(double mScore, bool isCriticalRisk = false, TradeType tradeType = TradeType.ShortIronCondor)
        { 
            if (tradeType == TradeType.ShortIronCondor) 
                return mScore switch {
                   >= 0.8 => isCriticalRisk ? TradeRiskType.Critical : TradeRiskType.High,
                   >= 0.7 => isCriticalRisk ? TradeRiskType.Critical : TradeRiskType.Medium,
                   _ => TradeRiskType.Low
               };
            else
                return mScore switch {
                    >= 0.8 => TradeRiskType.Low,
                    >= 0.7 => isCriticalRisk ? TradeRiskType.Critical : TradeRiskType.Medium,
                    _ => isCriticalRisk ? TradeRiskType.Critical : TradeRiskType.High
                };
        }

        public bool IsValid => OrderId > 0 && TradeId > 0;
    }
}
