using System;
using System.Linq;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Util;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.AlgoTrader;
using TomasAI.IFM.Application.Blackboard;

namespace TomasAI.IFM.Service.AlgoTrader.Model.LongIronCondor
{
    public class LongIronCondorTradePlan : TradePlan, ITradeAlgorithm
    {
        LossProbabilityViewModel _lossProbability;
        TradePriceReadModel _tradePrice;
        SignalProcessor<LongIronCondorTradePlan> _sp;

        public LongIronCondorTradePlan(TradePlanReadModel e)
           : base(e)
        {
        }

        public LongIronCondorTradePlan(DateTime valueDate, IOptionTradeCollection optionTrades, FuturesEodDataViewModel futuresEodData, FuturesTradeSignalViewModel futuresTradeSignal, IBlackboardService blackboardService, DateTime createdOn, string createdBy)
            : base(
                orderId: optionTrades.PrimaryTrade?.OrderId ?? 0,
                tradeId: optionTrades.PrimaryTrade?.TradeId ?? 0,
                tradeType: optionTrades.PrimaryTrade.TradeType,
                tradeDate: optionTrades.PrimaryTrade?.TradeDate ?? DateTime.MinValue,
                valueDate: valueDate,
                maturityDate: optionTrades.PrimaryTrade?.MaturityDate ?? DateTime.MinValue,
                actionDate: DateTime.Now,
                actionType: ActionType.HoldTradePosition,
                actionSubType: ActionSubType.TradeInProfitPosition,
                actionState: ActionState.Normal,
                actionReason: string.Empty,
                tradePnl: optionTrades.PrimaryTrade?.TradePositions.GetTradePnl() ?? 0m,
                forwardLossRatio: optionTrades.PrimaryTrade?.TradePositions.GetFowardLossRatio(optionTrades.PrimaryTrade.TradeType, TradeStatus.IntraDay, optionTrades.PrimaryTrade.TradeLimit.MinProfitLimit) ?? 0.0,
                lossProbability: optionTrades.PrimaryTrade?.TradePositions.Get(optionTrades.PrimaryTrade.TradeType, OptionType.Put, TradeStatus.IntraDay).LossProbability ?? 0.0,
                mscore: 0,
                maxProfit: optionTrades.PrimaryTrade?.TradeLimit.MaxProfit ?? 0m,
                maxLoss: optionTrades.PrimaryTrade?.TradeLimit.MaxLoss ?? 0m,
                minProfitTarget: optionTrades.PrimaryTrade?.TradeLimit.MinProfitTarget ?? 0m,
                dailyProfitTarget: optionTrades.PrimaryTrade?.TradeLimit.DailyProfitTarget ?? 0m,
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
                putOTMProbability: optionTrades.PrimaryTrade?.TradePositions.Get(optionTrades.PrimaryTrade.TradeType, OptionType.Put, TradeStatus.IntraDay)?.OTMProbability ?? 0.0,
                callOTMProbability: optionTrades.PrimaryTrade?.TradePositions.Get(optionTrades.PrimaryTrade.TradeType, OptionType.Call, TradeStatus.IntraDay)?.OTMProbability ?? 0.0,
                shortPutGamma: optionTrades.PrimaryTrade.TradePositions.Get(optionTrades.PrimaryTrade.TradeType, OptionType.Put, TradeStatus.IntraDay).OptionLegData.Get(OptionLegAction.Short, OptionType.Put)?.Gamma ?? 0.0,
                shortCallGamma: optionTrades.PrimaryTrade.TradePositions.Get(optionTrades.PrimaryTrade.TradeType, OptionType.Call, TradeStatus.IntraDay).OptionLegData.Get(OptionLegAction.Short, OptionType.Call)?.Gamma ?? 0.0,
                gammaRisk: GammaRiskType.None,
                netPrice: optionTrades.PrimaryTrade?.TradePositions.GetNetSpread(optionTrades.PrimaryTrade.TradeType, TradeStatus.IntraDay) ?? 0m,
                forwardPrice: optionTrades.PrimaryTrade?.TradePositions.GetForwardPrice(optionTrades.PrimaryTrade.TradeType, TradeStatus.IntraDay) ?? 0m,
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
                Value: LongMDIWarningReached,
                WarningLimit: LongMDIWarningReached * 2 * Convert.ToDouble(openingNetSpread),
                MaxLimit: LongMDILimitReached * 2 * Convert.ToDouble(openingNetSpread));
            blackboardService.IronCondorMDILimit.Set(optionTrades.PrimaryTrade.Id, valueDate, ironCondorMDILimit);
        }

        public decimal LossThreshold => _lossProbability.Threshold;
        public int LossThresholdCount => _lossProbability.ThresholdCount;
        public TradePriceReadModel TradePrice => _tradePrice;
        public override decimal AverageTradePnl => _sp == null ? TradePlan.TradePnl : Convert.ToDecimal(_sp.Average(TradePlan, 10, e => Convert.ToDouble(e.TradePnl)));
        public override string ContractId => FuturesEodData.ContractId;
    
        public static TradePlan Create(DateTime valueDate, IOptionTradeCollection optionTrades, FuturesEodDataViewModel futuresEodData, FuturesTradeSignalViewModel futuresTradeSignal, IBlackboardService blackboardService, DateTime createdOn, string createdBy)
        {
            var ironCondorTrades = new OptionTradeCollection(optionTrades.Where(e => e.TradeType == TradeType.ShortIronCondor || e.TradeType == TradeType.LongIronCondor));
            if (ironCondorTrades.Count > 0)
                return new LongIronCondorAlgorithm(valueDate, ironCondorTrades, futuresEodData, futuresTradeSignal, blackboardService, createdOn, createdBy);
            return default;
        }

        public LongIronCondorTradePlan TradePlan => this;

        public LongIronCondorTradePlan SetLossProbability(Func<double, LossProbabilityViewModel> lossProbabilityEstimator)
        {
            _lossProbability = lossProbabilityEstimator(ForwardLossRatio);
            return this;
        }

        public LongIronCondorTradePlan SetSignalProcessor(Func<SignalProcessor<LongIronCondorTradePlan>> getSignalProcessor)
        {
            _sp = getSignalProcessor();
            return this;
        }

        public LongIronCondorTradePlan SetTradePrice(Func<TradePriceReadModel> getTradePrice)
        {
            _tradePrice = null;
            return this;
        }

        public LongIronCondorTradePlan SetStopLossLimit(Func<TradePlanStopLossLimitReadModel> getStopLossLimit)
        {
            SetStopLossLimit(getStopLossLimit().StopLossLimit);
            return this;
        }

        public LongIronCondorTradePlan SetFundBalance(Func<decimal> getFundBalance)
        {
            SetFundBalance(getFundBalance());
            return this;
        }

        public LongIronCondorTradePlan SetForwardDelta(Func<DateTime, TradeType, double> getForwardDelta)
        {
            SetForwardDelta(getForwardDelta(ValueDate, TradeType));
            return this;
        }

        public LongIronCondorTradePlan SetForwardLossLimitType(Func<TradePlanForwardLossLimitId, ForwardLossLimitType> getForwardLossLimitType)
        {
            var id = TradePlanForwardLossLimitId.Create(OrderId, TradeId, TradeType, ValueDate);
            SetForwardLossLimitType(getForwardLossLimitType(id));
            return this;
        }

        public override TradePlanUpdatedEvent ExecuteAlgorithm()
        {
            throw new NotImplementedException();
        }
    }
}
