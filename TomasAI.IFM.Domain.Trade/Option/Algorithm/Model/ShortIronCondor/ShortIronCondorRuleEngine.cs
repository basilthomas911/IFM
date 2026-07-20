using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Domain.Trade.Option.Algorithm.Model.ShortIronCondor;

public class ShortIronCondorRuleEngine : TradePlanRuleEngine<ShortIronCondorAlgorithm>
{
    public ShortIronCondorRuleEngine(ShortIronCondorAlgorithm tradePlan) : base(tradePlan)
    {
        var shortIronCondorRules = new TradePlanRule<ShortIronCondorAlgorithm>[]
        {
            new ShortIronCondorRule(ActionSubType.RaiseTrailingStopLimit)
                .Match(e => e.TradeInProfitPosition && e.RaiseTrailingStopLimit)
                .Execute(e => e.IncrementStopLossLimit()
                .SetWarning($"Trailing Stop Limit Raised: {e.MinTrailingStopLimit:P2} @ {e.TrailingStopLimit:C} := hold trade")
                .WarnTradePosition(ActionSubType.RaiseTrailingStopLimit)),
            new ShortIronCondorRule(ActionSubType.TrailingStopLimitReached)
                .Match(e => e.TradeInProfitPosition && e.InTrailingStopLimit && e.TrailingStopLimitReached)
                .Execute(e => e.SetRedAlert($"Trailing Stop Limit Reached: {e.MinTrailingStopLimit:P2} @ {e.TrailingStopLimit:C} := exit trade @ ask price")
                .ClearStopLossLimit()
                .ExitTradePosition(ActionSubType.TrailingStopLimitReached)),
            new ShortIronCondorRule(ActionSubType.InTrailingStop)
                .Match(e => e.TradeInProfitPosition && e.InTrailingStopLimit)
                .Execute(e => e.SetWarning($"In Trailing Stop: {e.MinTrailingStopLimit:P2} @ {e.TrailingStopLimit:C} := hold trade")
                .WarnTradePosition(ActionSubType.InTrailingStop)),
            new ShortIronCondorRule(ActionSubType.TradeInProfitPosition)
                .Match(e => e.TradeInProfitPosition)
                .Execute(e => e.SetNormal($"Trend Direction is {e.TrendDirection} and Trend Strength is {e.TrendStrength} := hold trade")
                .HoldTradePosition(ActionSubType.TradeInProfitPosition)),
            new ShortIronCondorRule(ActionSubType.ClearStopLossLimit)
                .Match(e => e.TradeInLossPosition && e.InTrailingStopLimit)
                .Execute(e => e.ClearStopLossLimit()
                .SetNormal("Clear Trailing Stop Limit := hold trade")
                .HoldTradePosition(ActionSubType.ClearStopLossLimit)),
            new ShortIronCondorRule(ActionSubType.ForwardLossRiskLimitReached)
                .Match(e => e.TradeInLossPosition && e.ForwardLossRatio >= e.ShortMDILimitReached && e.ForwardLossLimitReached)
                .Execute(e => e.SetRedAlert($"Forward Loss Risk Exceeded := exit trade @ ask price")
                .ExitTradePosition(ActionSubType.ForwardLossRiskLimitReached)),
            new ShortIronCondorRule(ActionSubType.ForwardLossRiskLimitReachedWarning)
                .Match(e => e.TradeInLossPosition && e.ForwardLossRatio >= e.ShortMDILimitReached && !e.ForwardLossLimitReached)
                .Execute(e => e.SetCritical($"Forward Loss Risk Exceeded Warning := exit trade @ mid price")
                .WarnTradePosition(ActionSubType.ForwardLossRiskLimitReachedWarning)),
            new ShortIronCondorRule(ActionSubType.ForwardLossRiskLimitWarning)
                .Match(e => e.TradeInLossPosition && e.ForwardLossRatio >= e.ShortMDIWarningReached)
                .Execute(e => e.SetWarning($"Forward Loss Risk Warning := hold trade")
                .WarnTradePosition(ActionSubType.ForwardLossRiskLimitWarning)),
            new ShortIronCondorRule(ActionSubType.TradeInLossPosition)
                .Match(e => e.TradeInLossPosition)
                .Execute(e => e.SetNormal($"Trend Direction is {e.TrendDirection} and Trend Strength is {e.TrendStrength} := hold trade")
                .HoldTradePosition(ActionSubType.TradeInLossPosition)),
            new ShortIronCondorRule(ActionSubType.MarketRevertingToDownTrend)
                .Match(e => e.MarketIsRevertingToDownTrend)
                .Execute(e => e.SetWarning($"Market Reverting to DownTrend := exit trade @ mid price")
                .WarnTradePosition(ActionSubType.MarketRevertingToDownTrend)),
            new ShortIronCondorRule(ActionSubType.HighShortCallGammaRisk)
                .Match(e => e.IsHighShortGammaRisk)
                .Execute(e => e.SetRedAlert($"Short Gamma Risk Is High := exit trade @ ask price")
                .ExitTradePosition(ActionSubType.HighShortCallGammaRisk)),
            new ShortIronCondorRule(ActionSubType.LowShortCallGammaRisk)
                .Match(e => e.IsLowShortGammaRisk)
                .Execute(e => e.SetWarning($"Short Gamma Risk Is Low := hold trade")
                .HoldTradePosition(ActionSubType.LowShortCallGammaRisk)),
            new ShortIronCondorRule(ActionSubType.DailyProfitTargetExceeded)
                .Match(e => e.TradeInProfitPosition && e.DailyProfitTargetExceeded)
                .Execute(e => e.SetWarning($"Daily Profit Target Exceeded: {e.AverageTradePnl:C2} > {e.DailyProfitTarget:C2} := exit trade @ mid price")
                .WarnTradePosition(ActionSubType.DailyProfitTargetExceeded)),
            new ShortIronCondorRule(ActionSubType.MaxLossLimitReached)
                .Match(e => e.TradeInLossPosition && e.MaxLossLimitReached)
                .Execute(e => e.SetRedAlert($"Max Loss Limit Reached: {e.AverageTradePnl:C2} > {-e.MaxLoss:C2} := exit trade @ ask price")
                .ExitTradePosition(ActionSubType.MaxLossLimitReached)),
        };
        AddRules(shortIronCondorRules);
    }
}

internal class ShortIronCondorRule(ActionSubType ruleName) : TradePlanRule<ShortIronCondorAlgorithm>(ruleName)
{
}
