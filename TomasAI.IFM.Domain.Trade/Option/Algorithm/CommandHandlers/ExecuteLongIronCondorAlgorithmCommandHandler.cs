using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.TradeAlgorithm.Commands;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.TradeAlgorithm.Events;

namespace TomasAI.IFM.Domain.Trade.Option.Algorithm.CommandHandlers;

public class ExecuteLongIronCondorAlgorithmCommandHandler 
    : BaseBoundedContextCommandHandler<OptionTradeAlgorithmBoundedContextState>,
     IBoundedContextCommandHandler<ExecuteLongIronCondorAlgorithmCommand, OptionTradeAlgorithmBoundedContextState>
{
   
    public bool Execute(ExecuteLongIronCondorAlgorithmCommand e, OptionTradeAlgorithmBoundedContextState state)
        => ExecuteLongIronCondorAlgorithm(e, state);

    /// <summary>
    /// Executes the Long Iron Condor trading algorithm based on the specified command and the current state of the
    /// option trade algorithm.
    /// </summary>
    /// <remarks>This method evaluates the provided command against a set of predefined rules in the rule
    /// engine. Based on the matching rule, it executes the corresponding action to generate a trade plan. If the
    /// generated trade plan differs from the current state, the state is updated with the new trade plan and an event
    /// is raised to reflect the execution of the algorithm.</remarks>
    /// <param name="e">The command containing the details required to execute the Long Iron Condor algorithm, including the action type
    /// and metadata.</param>
    /// <param name="state">The current bounded context state of the option trade algorithm, which provides the rule engine and tracks trade plan
    /// changes.</param>
    /// <returns><see langword="true"/> if the trade plan was updated and the corresponding event was successfully applied to the
    /// state; otherwise, <see langword="false"/>.</returns>
    static bool ExecuteLongIronCondorAlgorithm(ExecuteLongIronCondorAlgorithmCommand e, OptionTradeAlgorithmBoundedContextState state)
    {
        var rule = state.GetRuleEngine(e);
        var tradePlan = e switch
        {
            _ when rule.Match(ActionSubType.RaiseTrailingStopLimit) => rule.Execute(ActionSubType.RaiseTrailingStopLimit),
            _ when rule.Match(ActionSubType.TrailingStopLimitReached) => rule.Execute(ActionSubType.TrailingStopLimitReached),
            _ when rule.Match(ActionSubType.InTrailingStop) => rule.Execute(ActionSubType.InTrailingStop),
            _ when rule.Match(ActionSubType.DailyProfitTargetExceeded) => rule.Execute(ActionSubType.DailyProfitTargetExceeded),
            _ when rule.Match(ActionSubType.TradeInProfitPosition) => rule.Execute(ActionSubType.TradeInProfitPosition),
            _ when rule.Match(ActionSubType.ClearStopLossLimit) => rule.Execute(ActionSubType.ClearStopLossLimit),
            _ when rule.Match(ActionSubType.MaxLossLimitReached) => rule.Execute(ActionSubType.MaxLossLimitReached),
            _ when rule.Match(ActionSubType.ForwardLossRiskLimitReached) => rule.Execute(ActionSubType.ForwardLossRiskLimitReached),
            _ when rule.Match(ActionSubType.ForwardLossRiskLimitReachedWarning) => rule.Execute(ActionSubType.ForwardLossRiskLimitReachedWarning),
            _ when rule.Match(ActionSubType.ForwardLossRiskLimitWarning) => rule.Execute(ActionSubType.ForwardLossRiskLimitWarning),
            //_ when _rule.Match(ActionSubType.MarketRevertingToUpTrend) => _rule.Execute(ActionSubType.MarketRevertingToUpTrend),
            //_ when _rule.Match(ActionSubType.MDIWatermarkLimitReached) => _rule.Execute(ActionSubType.MDIWatermarkLimitReached),
            _ when rule.Match(ActionSubType.TradeInLossPosition) => rule.Execute(ActionSubType.TradeInLossPosition),
            _ => rule.SetNormal()
        };
        var tradePlanVM = tradePlan.ToViewModel(e.OriginatedOn, e.OriginatedBy);
        if (state.HasTradePlanChanged(tradePlanVM))
            return state.Update(new LongIronCondorAlgorithmExecutedEvent { TradeAlgorithmId = e.EntityId, TradePlan = tradePlanVM }, e);
        return false;
    }

}
