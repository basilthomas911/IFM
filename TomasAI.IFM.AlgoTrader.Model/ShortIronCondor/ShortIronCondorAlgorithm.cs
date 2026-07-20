using System;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.AlgoTrader;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Application.Blackboard;

namespace TomasAI.IFM.Service.AlgoTrader.Model.ShortIronCondor
{
    public class ShortIronCondorAlgorithm : ShortIronCondorTradePlan, ITradeAlgorithm
    {
        ShortIronCondorRuleEngine _rule;

        public ShortIronCondorAlgorithm(TradePlanReadModel e)
           : base(e)
        {
            _rule = new(this);
        }

        public ShortIronCondorAlgorithm(DateTime valueDate, IOptionTradeCollection optionTrades, FuturesEodDataViewModel futuresEodData, FuturesTradeSignalViewModel futuresTradeSignal, IBlackboardService blackboardService, DateTime createdOn, string createdBy)
            : base(valueDate, optionTrades, futuresEodData, futuresTradeSignal, blackboardService, createdOn, createdBy)
        {
            _rule = new(this);
        }

        /// <summary>
        /// evaluate short iron condor trade strategy
        /// </summary>
        /// <returns></returns>
        public override TradePlanUpdatedEvent ExecuteAlgorithm()
        {

            TradePlan tradePlan;
            try
            {
                tradePlan = default(TradePlan) switch
                {
                    _ when _rule.Match(ActionSubType.RaiseTrailingStopLimit) => _rule.Execute(ActionSubType.RaiseTrailingStopLimit),
                    _ when _rule.Match(ActionSubType.TrailingStopLimitReached) => _rule.Execute(ActionSubType.TrailingStopLimitReached),
                    _ when _rule.Match(ActionSubType.InTrailingStop) => _rule.Execute(ActionSubType.InTrailingStop),
                    _ when _rule.Match(ActionSubType.DailyProfitTargetExceeded) => _rule.Execute(ActionSubType.DailyProfitTargetExceeded),
                    _ when _rule.Match(ActionSubType.TradeInProfitPosition) => _rule.Execute(ActionSubType.TradeInProfitPosition),

                    _ when _rule.Match(ActionSubType.ClearStopLossLimit) => _rule.Execute(ActionSubType.ClearStopLossLimit),
                    _ when _rule.Match(ActionSubType.MaxLossLimitReached) => _rule.Execute(ActionSubType.MaxLossLimitReached),
                    _ when _rule.Match(ActionSubType.ForwardLossRiskLimitReached) => _rule.Execute(ActionSubType.ForwardLossRiskLimitReached),
                    _ when _rule.Match(ActionSubType.ForwardLossRiskLimitReachedWarning) => _rule.Execute(ActionSubType.ForwardLossRiskLimitReachedWarning),
                    _ when _rule.Match(ActionSubType.ForwardLossRiskLimitWarning) => _rule.Execute(ActionSubType.ForwardLossRiskLimitWarning),
                    //_ when _rule.Match(ActionSubType.MarketRevertingToDownTrend) => _rule.Execute(ActionSubType.MarketRevertingToDownTrend),
                    //_ when _rule.Match(ActionSubType.MDIWatermarkLimitReached) => _rule.Execute(ActionSubType.MDIWatermarkLimitReached),
                    _ when _rule.Match(ActionSubType.TradeInLossPosition) => _rule.Execute(ActionSubType.TradeInLossPosition),
                    _ => SetNormal($"Trend Direction is {TrendDirection} and Trend Strength is {TrendStrength} := hold trade").HoldTradePosition(ActionSubType.None)
                };
            }
            catch (Exception ex)
            {
                tradePlan = SetNormal($"{GetType().Name}: {ex.GetType().Name} - {ex.Message}").HoldTradePosition(ActionSubType.Error);
            }
            return new TradePlanUpdatedEvent { TradePlan = tradePlan.ToViewModel() };

        }

    }

}
