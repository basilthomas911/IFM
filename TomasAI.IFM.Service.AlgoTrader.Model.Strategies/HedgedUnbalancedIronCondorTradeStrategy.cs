using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reactive;
using TomasAI.IFM.Service.AlgoTrader.Reactive;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.AlgoTrader.Model;

namespace TomasAI.IFM.Service.AlgoTrader.Model.Strategies
{
    /// <summary>
    /// trade strategy type information
    /// </summary>
    public class HedgedUnbalancedIronCondorTradeStrategyType : ITradeStrategyType
    {
        public const string RaiseTrailingStopLimit = "RaiseTrailingStopLimit";

        private ImmutableList<ITradeStrategyRuleInfo> Rules => ImmutableList.Create<ITradeStrategyRuleInfo>()
           .AddRange(new ITradeStrategyRuleInfo[] {
                new TradeStrategyRuleInfo(RaiseTrailingStopLimit, "raise trailing stop limit when trade pnl has increased by a set increase over current minimum profit level")
           });

        /// <summary>
        /// trade strategy name for lookup purposes
        /// </summary>
        public string Name => this.GetType().Name.Replace("TradeStrategyType", string.Empty);

        /// <summary>
        /// full description of trade strategy
        /// </summary>
        public string Description => "An unbalanced hedging Iron Condor trading strategy";

        public TradeType TradeType => TradeType.ShortIronCondor;

        public ITradeStrategyRuleInfo GetRuleInfo(string ruleName) => Rules.Where(e => e.Name == ruleName).Single();

    }

    public class HedgedUnbalancedIronCondorTradeStrategy : BaseTradeStrategy<HedgedUnbalancedIronCondorTradeStrategyType, IronCondorTradePlan>
    {
        private HedgedUnbalancedIronCondorTradeStrategyType _strategyType = new HedgedUnbalancedIronCondorTradeStrategyType();

        public HedgedUnbalancedIronCondorTradeStrategy(IEventProducer eventProducer) :base(eventProducer)
        {

        }

        public ITradeStrategyRuleInfo RaiseTrailingStopLimit => _strategyType.GetRuleInfo(HedgedUnbalancedIronCondorTradeStrategyType.RaiseTrailingStopLimit);

       
    }
}
