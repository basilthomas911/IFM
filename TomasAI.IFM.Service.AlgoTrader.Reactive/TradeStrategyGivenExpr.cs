using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.AlgoTrader.Model;

namespace TomasAI.IFM.Service.AlgoTrader.Reactive
{
    public class TradeStrategyGivenExpr<TName, TSource> : ITradeStrategyGivenExpr<TName, TSource> where TName : ITradeStrategyType where TSource : TradePlan
    {
        public TradeStrategyGivenExpr(TradeStrategyContext<TName, TSource> context)
        {
            Context = context;
        }

        /// <summary>
        /// strategy execution context
        /// </summary>
        public TradeStrategyContext<TName, TSource> Context { get; }

        /// <summary>
        /// set strategy output expression
        /// </summary>
        /// <param name="updateTradePlan">strategy output expression</param>
        /// <returns></returns>
        public ITradeStrategyOutputExpr<TName, TSource> Output(Action<TSource> updateTradePlan)
        {
            Context.Output = updateTradePlan;
            return null;
        }
    }
}
