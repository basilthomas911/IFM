using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.TradeAlgorithm.Commands;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Domain.Trade.Option.Algorithm.Model;
using TomasAI.IFM.Shared.TradeAlgorithm.Events;

namespace TomasAI.IFM.Domain.Trade.Option.Algorithm.CommandHandlers;

public class ExecuteShortIronCondorAlgorithmCommandHandler : BaseBoundedContextCommandHandler<OptionTradeAlgorithmBoundedContextState>,
             IBoundedContextCommandHandler<ExecuteShortIronCondorAlgorithmCommand, OptionTradeAlgorithmBoundedContextState>
{
    
    public bool Execute(ExecuteShortIronCondorAlgorithmCommand e, OptionTradeAlgorithmBoundedContextState state)
        => ExecuteShortIronCondorAlgorithm(e, state);

    /// <summary>
    /// Executes the Short Iron Condor trading algorithm based on the specified command and the current state of the
    /// option trade algorithm.
    /// </summary>
    /// <remarks>This method evaluates the provided command against a set of predefined rules in the rule
    /// engine. Based on the matching rule, it executes the corresponding action and generates a trade plan. If an
    /// exception occurs during rule execution, the trade plan is set to an error state. The method then converts the
    /// trade plan to a view model and updates the state if the trade plan has changed.</remarks>
    /// <param name="e">The command containing the details required to execute the Short Iron Condor algorithm, including the action
    /// type and metadata.</param>
    /// <param name="state">The bounded context state of the option trade algorithm, which provides the rule engine and manages state transitions.</param>
    /// <returns><see langword="true"/> if the trade plan has changed and the state was successfully updated; otherwise, <see
    /// langword="false"/>.</returns>
    static bool ExecuteShortIronCondorAlgorithm(ExecuteShortIronCondorAlgorithmCommand e, OptionTradeAlgorithmBoundedContextState state)
    {
        var rule = state.GetRuleEngine(e);
        TradePlan tradePlan;
        try
        {
            tradePlan = e switch
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
                //_ when _rule.Match(ActionSubType.MarketRevertingToDownTrend) => _rule.Execute(ActionSubType.MarketRevertingToDownTrend),
                //_ when _rule.Match(ActionSubType.MDIWatermarkLimitReached) => _rule.Execute(ActionSubType.MDIWatermarkLimitReached),
                _ when rule.Match(ActionSubType.TradeInLossPosition) => rule.Execute(ActionSubType.TradeInLossPosition),
                _ => rule.SetNormal()
            };
        }
        catch (Exception ex)
        {
            tradePlan = rule.SetError(ex, "ExecuteShortIronCondorAlgorithm");
        }
        var tradePlanVM = tradePlan.ToViewModel(e.OriginatedOn, e.OriginatedBy);
        return state.HasTradePlanChanged(tradePlanVM) && 
                state.Update(new ShortIronCondorAlgorithmExecutedEvent { TradeAlgorithmId = e.EntityId, TradePlan = tradePlanVM }, e);
    }
}

    
