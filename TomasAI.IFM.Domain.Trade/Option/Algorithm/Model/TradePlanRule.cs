using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Domain.Trade.Option.Algorithm.Model
{
    public class TradePlanRule<TAlgo> where TAlgo:TradePlan
    {
        ActionSubType _ruleName;
        Func<TAlgo, bool>? _ruleMatchExpression;
        Func<TradePlan, TradePlan>? _ruleExecuteExpression;

        /// <summary>
        /// create trade plan rule
        /// </summary>
        /// <param name="ruleName"></param>
        public TradePlanRule(ActionSubType ruleName)
        {
            _ruleName = ruleName;
        }

        public ActionSubType RuleName => _ruleName;

        /// <summary>
        /// set trade plan rule match expression
        /// </summary>
        /// <param name="ruleMatchExpression"></param>
        /// <returns></returns>
        public TradePlanRule<TAlgo> Match(Func<TAlgo, bool> ruleMatchExpression)
        {
            _ruleMatchExpression = ruleMatchExpression;
            return this;
        }

        /// <summary>
        /// set trade plan rule execute expression
        /// </summary>
        /// <param name="ruleExecuteExpression"></param>
        /// <returns></returns>
        public TradePlanRule<TAlgo> Execute(Func<TradePlan, TradePlan> ruleExecuteExpression)
        {
            _ruleExecuteExpression = ruleExecuteExpression;
            return this;
        }

        /// <summary>
        /// check if rule matches rule match expression
        /// </summary>
        /// <param name="tradePlan"></param>
        /// <returns></returns>
        public bool Match(TAlgo tradePlan)
            => (_ruleMatchExpression ?? throw new InvalidOperationException("Rule match expression is not configured.")).Invoke(tradePlan);

        /// <summary>
        /// execute rule on rule match
        /// </summary>
        /// <param name="tradePlan"></param>
        /// <returns></returns>
        public TradePlan Execute(TradePlan tradePlan)
            => (_ruleExecuteExpression ?? throw new InvalidOperationException("Rule execute expression is not configured.")).Invoke(tradePlan);

    }
}
