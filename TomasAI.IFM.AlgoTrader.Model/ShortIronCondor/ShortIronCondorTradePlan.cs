using System;
using System.Linq;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Util;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.AlgoTrader;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Application.Blackboard;

namespace TomasAI.IFM.Service.AlgoTrader.Model.ShortIronCondor
{
    public class ShortIronCondorTradePlan : TradePlan, ITradeAlgorithm
    {
        LossProbabilityViewModel _lossProbability;
        TradePriceReadModel _tradePrice;
        SignalProcessor<ShortIronCondorTradePlan> _sp;

        public static TradePlan Create(DateTime valueDate, IOptionTradeCollection optionTrades, FuturesEodDataViewModel futuresEodData, FuturesTradeSignalViewModel futuresTradeSignal, IBlackboardService blackboardService, DateTime createdOn, string createdBy)
        {
            var ironCondorTrades = new OptionTradeCollection(optionTrades.Where(e => e.TradeType == TradeType.ShortIronCondor || e.TradeType == TradeType.LongIronCondor));
            if (ironCondorTrades.Count > 0)
                return new ShortIronCondorAlgorithm(valueDate, ironCondorTrades, futuresEodData, futuresTradeSignal, blackboardService, createdOn, createdBy);
            return default;
        }

        public ShortIronCondorTradePlan(TradePlanReadModel e)
           : base(e)
        {
        }

        public ShortIronCondorTradePlan(DateTime valueDate, IOptionTradeCollection optionTrades, FuturesEodDataViewModel futuresEodData, FuturesTradeSignalViewModel futuresTradeSignal, IBlackboardService blackboardService, DateTime createdOn, string createdBy)
            : base(
                orderId: optionTrades.PrimaryTrade.OrderId,
                tradeId: optionTrades.PrimaryTrade.TradeId,
                tradeType: optionTrades.PrimaryTrade.TradeType,
                tradeDate: optionTrades.PrimaryTrade.TradeDate,
                valueDate: valueDate,
                maturityDate: optionTrades.PrimaryTrade.MaturityDate,
                actionDate: DateTime.Now,
                actionType: ActionType.HoldTradePosition,
                actionSubType: ActionSubType.TradeInProfitPosition,
                actionState: ActionState.Normal,
                actionReason: string.Empty,
                tradePnl: optionTrades.PrimaryTrade.TradePositions.GetTradePnl(),
                forwardLossRatio: optionTrades.PrimaryTrade.TradePositions.GetFowardLossRatio(optionTrades.PrimaryTrade.TradeType, TradeStatus.IntraDay, optionTrades.PrimaryTrade.TradeLimit.MaxLossLimit),
                lossProbability: optionTrades.PrimaryTrade.TradePositions.Get(optionTrades.PrimaryTrade.TradeType, OptionType.Put, TradeStatus.IntraDay).LossProbability,
                mscore: 0,
                maxProfit: optionTrades.PrimaryTrade.TradeLimit.MaxProfit,
                maxLoss: optionTrades.PrimaryTrade.TradeLimit.MaxLoss,
                minProfitTarget: optionTrades.PrimaryTrade.TradeLimit.MinProfitTarget,
                dailyProfitTarget: optionTrades.PrimaryTrade.TradeLimit.DailyProfitTarget,
                assetPrice: Convert.ToDecimal(futuresEodData.ClosePrice),
                assetStdDev: futuresEodData.DailyStdDev,
                assetMean: futuresEodData.Mean,
                assetPriceChange: futuresEodData.DailyPercentChange,
                marketTrend: futuresEodData.MarketDirection,
                marketVolatility: futuresEodData.MarketVolatility,
                marketDirection: futuresEodData.PriceDirection,
                vixVolatility: futuresEodData.PriceVolatility,
                fiftyDayMA: 0,
                fiveDayXMA: 0,
                putOTMProbability: optionTrades.PrimaryTrade.TradePositions.Get(optionTrades.PrimaryTrade.TradeType, OptionType.Put, TradeStatus.IntraDay)?.OTMProbability ?? 0,
                callOTMProbability: optionTrades.PrimaryTrade.TradePositions.Get(optionTrades.PrimaryTrade.TradeType, OptionType.Call, TradeStatus.IntraDay)?.OTMProbability ?? 0.0,
                shortPutGamma: optionTrades.PrimaryTrade.TradePositions.Get(optionTrades.PrimaryTrade.TradeType, OptionType.Put, TradeStatus.IntraDay).OptionLegData.Get(OptionLegAction.Short, OptionType.Put)?.Gamma ?? 0.0,
                shortCallGamma: optionTrades.PrimaryTrade.TradePositions.Get(optionTrades.PrimaryTrade.TradeType, OptionType.Call, TradeStatus.IntraDay).OptionLegData.Get(OptionLegAction.Short, OptionType.Call)?.Gamma ?? 0.0,
                gammaRisk: GammaRiskType.None,
                netPrice: optionTrades.PrimaryTrade.TradePositions.GetNetSpread(optionTrades.PrimaryTrade.TradeType, TradeStatus.IntraDay),
                forwardPrice: optionTrades.PrimaryTrade.TradePositions.GetForwardPrice(optionTrades.PrimaryTrade.TradeType, TradeStatus.IntraDay),
                forwardDelta: 0.0,
                stopLossLimit: 0.0,
                createdOn: createdOn,
                createdBy: createdBy)
        {
            SetFuturesTradeSignal(futuresTradeSignal);
            SetFuturesEodData(futuresEodData);
            var openingNetSpread = optionTrades.PrimaryTrade.TradePositions.GetNetSpread(optionTrades.PrimaryTrade.TradeType, TradeStatus.Open);
            var ironCondorMDILimit = new IronCondorMDILimitViewModel(
                Id: optionTrades.PrimaryTrade.Id,
                ValueDate: valueDate,
                Value: ShortMDIWarningReached,
                WarningLimit: ShortMDIWarningReached * 2 * Convert.ToDouble(openingNetSpread),
                MaxLimit: ShortMDILimitReached * 2 * Convert.ToDouble(openingNetSpread));
            blackboardService.IronCondorMDILimit.Set(optionTrades.PrimaryTrade.Id, valueDate, ironCondorMDILimit);
            var callGamma = Convert.ToInt32(ShortCallGamma * 100000);
            var putGamma = Convert.ToInt32(ShortPutGamma * 100000);
            if (callGamma == 0 || putGamma == 0)
                GammaRisk = GammaRiskType.None;
            else
            {
                var callGammaDiff = Math.Abs(callGamma - putGamma);
                GammaRisk = callGammaDiff switch
                {
                    >= 30 => GammaRiskType.HighShortCallGamma,
                    >= 20 => GammaRiskType.LowShortCallGamma,
                    _ => GammaRiskType.None
                };
            }
        }

        public decimal LossThreshold => _lossProbability.Threshold;
        public int LossThresholdCount => _lossProbability.ThresholdCount;
        public TradePriceReadModel TradePrice => _tradePrice;
        public override decimal AverageTradePnl => _sp == null ? TradePlan.TradePnl : Convert.ToDecimal(_sp.Average(TradePlan, 10, e => Convert.ToDouble(e.TradePnl)));
        public override string ContractId => FuturesEodData.ContractId;
        public bool IsHighShortGammaRisk => GammaRisk == GammaRiskType.HighShortCallGamma;
        public bool IsLowShortGammaRisk => GammaRisk == GammaRiskType.LowShortCallGamma;

     
        public ShortIronCondorTradePlan TradePlan => this;

        public ShortIronCondorTradePlan SetLossProbability(Func<double, LossProbabilityViewModel> lossProbabilityEstimator)
        {
            _lossProbability = lossProbabilityEstimator(ForwardLossRatio);
            return this;
        }

        public ShortIronCondorTradePlan SetSignalProcessor(Func<SignalProcessor<ShortIronCondorTradePlan>> getSignalProcessor)
        {
            _sp = getSignalProcessor();
            return this;
        }

        public ShortIronCondorTradePlan SetTradePrice(Func<TradePriceReadModel> getTradePrice)
        {
            _tradePrice = null;
            return this;
        }

        public ShortIronCondorTradePlan SetStopLossLimit(Func<TradePlanStopLossLimitReadModel> getStopLossLimit)
        {
            SetStopLossLimit(getStopLossLimit().StopLossLimit);
            return this;
        }

        public ShortIronCondorTradePlan SetFundBalance(Func<decimal> getFundBalance)
        {
            SetFundBalance(getFundBalance());
            return this;
        }

        public ShortIronCondorTradePlan SetForwardDelta(Func<DateTime, TradeType, double> getForwardDelta)
        {
            SetForwardDelta(getForwardDelta(ValueDate, TradeType));
            return this;
        }

        public ShortIronCondorTradePlan SetForwardLossLimitType(Func<TradePlanForwardLossLimitId, ForwardLossLimitType> getForwardLossLimitType)
        {
            var id = TradePlanForwardLossLimitId.Create(OrderId, TradeId, TradeType, ValueDate);
            SetForwardLossLimitType(getForwardLossLimitType(id));
            return this;
        }

        /// <summary>
        /// evaluate short iron condor trade strategy
        /// </summary>
        /// <returns></returns>
        public override TradePlanUpdatedEvent ExecuteAlgorithm() => throw new NotImplementedException();

    }

}
