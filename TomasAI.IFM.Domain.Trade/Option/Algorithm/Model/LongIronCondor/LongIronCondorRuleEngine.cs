using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Domain.Trade.Option.Algorithm.Model.LongIronCondor;

public class LongIronCondorRuleEngine : TradePlanRuleEngine<LongIronCondorAlgorithm>
{
    public LongIronCondorRuleEngine(LongIronCondorAlgorithm tradePlan) : base(tradePlan)
    {
        var longIronCondorRules = new TradePlanRule<LongIronCondorAlgorithm>[]
        {
            new LongIronCondorRule(ActionSubType.RaiseTrailingStopLimit)
                .Match(e => e.TradeInProfitPosition && e.RaiseTrailingStopLimit)
                .Execute(e => e.IncrementStopLossLimit()
                .SetWarning($"Trailing Stop Limit Raised: {e.MinTrailingStopLimit:P2} @ {e.TrailingStopLimit:C} := hold trade")
                .HoldTradePosition(ActionSubType.RaiseTrailingStopLimit)),
            new LongIronCondorRule(ActionSubType.TrailingStopLimitReached)
                .Match(e => e.TradeInProfitPosition && e.InTrailingStopLimit && e.TrailingStopLimitReached)
                .Execute(e => e.SetRedAlert($"Trailing Stop Limit Reached: {e.MinTrailingStopLimit:P2} @ {e.TrailingStopLimit:C} := exit trade @ ask price")
                .ClearStopLossLimit()
                .ExitTradePosition(ActionSubType.TrailingStopLimitReached)),
            new LongIronCondorRule(ActionSubType.InTrailingStop)
                .Match(e => e.TradeInProfitPosition && e.InTrailingStopLimit)
                .Execute(e => e.SetWarning($"In Trailing Stop: {e.MinTrailingStopLimit:P2} @ {e.TrailingStopLimit:C} := hold trade")
                .WarnTradePosition(ActionSubType.InTrailingStop)),
            new LongIronCondorRule(ActionSubType.TradeInProfitPosition)
                .Match(e => e.TradeInProfitPosition)
                .Execute(e => e.SetNormal($"Trend Direction is {e.TrendDirection} and Trend Strength is {e.TrendStrength} := hold trade")
                .HoldTradePosition(ActionSubType.TradeInProfitPosition)),
            new LongIronCondorRule(ActionSubType.ClearStopLossLimit)
                .Match(e => e.TradeInLossPosition && e.InTrailingStopLimit)
                .Execute(e => e.ClearStopLossLimit()
                .SetNormal("Clear Trailing Stop Limit := hold trade")
                .HoldTradePosition(ActionSubType.ClearStopLossLimit)),
           new LongIronCondorRule(ActionSubType.ForwardLossRiskLimitReached)
                .Match(e => e.TradeInLossPosition && e.ForwardLossRatio <= e.LongMDILimitReached && e.ForwardLossLimitReached)
                .Execute(e => e.SetRedAlert($"Forward Loss Risk Exceeded := exit trade @ ask price")
                .ExitTradePosition(ActionSubType.ForwardLossRiskLimitReached)),
            new LongIronCondorRule(ActionSubType.ForwardLossRiskLimitReachedWarning)
                .Match(e => e.TradeInLossPosition && e.ForwardLossRatio <= e.LongMDILimitReached && !e.ForwardLossLimitReached)
                .Execute(e => e.SetCritical($"Forward Loss Risk Exceeded Warning := exit trade @ mid price")
                .WarnTradePosition(ActionSubType.ForwardLossRiskLimitReachedWarning)),
            new LongIronCondorRule(ActionSubType.ForwardLossRiskLimitWarning)
                .Match(e => e.TradeInLossPosition && e.ForwardLossRatio <= e.LongMDIWarningReached)
                .Execute(e => e.SetWarning($"Forward Loss Risk Warning := hold trade")
                .WarnTradePosition(ActionSubType.ForwardLossRiskLimitWarning)),
            new LongIronCondorRule(ActionSubType.TradeInLossPosition)
                .Match(e => e.TradeInLossPosition)
                .Execute(e => e.SetNormal($"Trend Direction is {e.TrendDirection} and Trend Strength is {e.TrendStrength} := hold trade")
                .HoldTradePosition(ActionSubType.TradeInLossPosition)),
            new LongIronCondorRule(ActionSubType.MarketRevertingToUpTrend)
                .Match(e => e.MarketIsRevertingToUpTrend)
                .Execute(e => e.SetWarning($"Market Reverting to UpTrend := exit trade @ mid price")
                .WarnTradePosition(ActionSubType.MarketRevertingToUpTrend)),
            new LongIronCondorRule(ActionSubType.MaxLossLimitReached)
                .Match(e => e.TradeInLossPosition && e.MaxLossLimitReached)
                .Execute(e => e.SetRedAlert($"Max Loss Limit Reached: {e.AverageTradePnl:C2} > {-e.MaxLoss:C2} := exit trade @ ask price")
                .ExitTradePosition(ActionSubType.MaxLossLimitReached)),
             new LongIronCondorRule(ActionSubType.DailyProfitTargetExceeded)
                .Match(e => e.TradeInProfitPosition && e.DailyProfitTargetExceeded)
                .Execute(e => e.SetCritical($"Daily Profit Target Exceeded: {e.AverageTradePnl:C2} > {e.DailyProfitTarget:C2} := exit trade @ mid price")
                .WarnTradePosition(ActionSubType.DailyProfitTargetExceeded)),
        };
        AddRules(longIronCondorRules);
    }
}

internal class LongIronCondorRule(ActionSubType ruleName)
    : TradePlanRule<LongIronCondorAlgorithm>(ruleName)
{
}
