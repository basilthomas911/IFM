using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Service.AlgoTrader.Model
{
    public class TradePlanRuleEngine<TAlgo> where TAlgo:TradePlan
    {
        readonly TAlgo _tradePlan;
        Dictionary<ActionSubType, TradePlanRule<TAlgo>> _tradePlanRules;

        public TradePlanRuleEngine(TAlgo tradePlan)
        {
            if (tradePlan is null) throw new ArgumentNullException(typeof(TAlgo).Name, "TradePlanRuleEngine: rule engine must be created with valid trade plan");
            _tradePlan = tradePlan;
        }

        /// <summary>
        /// add trade plan rules to rule engine
        /// </summary>
        /// <param name="tradePlanRules"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public void AddRules(TradePlanRule<TAlgo>[] tradePlanRules)
        {
            if (tradePlanRules is null || tradePlanRules.Length == 0) throw new ArgumentNullException("tradePlanRules", "TradePlanRuleEngine.AddRules: rule engine must be created with at least one rule");
            _tradePlanRules = new();
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
    }
}
