using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Domain.Trade.Option.Algorithm.Model;

public class TradePlanRuleEngine<TAlgo>(TAlgo tradePlan) where TAlgo:TradePlan
{
    readonly TAlgo _tradePlan = IsArgumentNull.Set(tradePlan);
    Dictionary<ActionSubType, TradePlanRule<TAlgo>> _tradePlanRules = [];

    /// <summary>
    /// add trade plan rules to rule engine
    /// </summary>
    /// <param name="tradePlanRules"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public void AddRules(TradePlanRule<TAlgo>[] tradePlanRules)
    {
        IsArgumentNull.Check(tradePlanRules, nameof(tradePlanRules));
        foreach (var tradePlanRule in tradePlanRules)
            _tradePlanRules.Add(tradePlanRule.RuleName, tradePlanRule);
    }

    /// <summary>
    /// check if trade rule matches
    /// </summary>
    /// <param name="ruleName"></param>
    /// <returns></returns>
    public bool Match(ActionSubType ruleName) => _tradePlanRules.ContainsKey(ruleName) && _tradePlanRules[ruleName].Match(_tradePlan);

    /// <summary>
    /// execute matching trade rule 
    /// </summary>
    /// <param name="ruleName"></param>
    /// <returns></returns>
    public TradePlan Execute(ActionSubType ruleName) => _tradePlanRules[ruleName].Execute(_tradePlan);

    public TradePlan SetNormal() => _tradePlan.SetNormal($"Trend Direction is {_tradePlan.TrendDirection} and Trend Strength is {_tradePlan.TrendStrength} := hold trade").HoldTradePosition(ActionSubType.None);
    public TradePlan SetError(Exception ex, string handlerName) => _tradePlan.SetNormal($"{handlerName}: {ex.Message}").HoldTradePosition(ActionSubType.Error);
}
