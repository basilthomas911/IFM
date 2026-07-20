using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.AlgoTrader.Model;

namespace TomasAI.IFM.Service.AlgoTrader.Reactive
{
    /// <summary>
    /// strategy rule expression
    /// </summary>
    /// <typeparam name="TName"></typeparam>
    /// <typeparam name="TSource"></typeparam>
    public class TradeStrategyRuleExpr<TName, TSource> : ITradeStrategyRuleExpr<TName, TSource> where TName : ITradeStrategyType where TSource : TradePlan
    {
        public TradeStrategyRuleExpr(TradeStrategyContext<TName, TSource> context)
        {
            Context = context;
        }

        /// <summary>
        /// strategy execution context
        /// </summary>
        public TradeStrategyContext<TName, TSource> Context { get ; }

        /// <summary>
        /// create an observable from a rx linq query function
        /// </summary>
        /// <param name="observableFunc"></param>
        /// <returns></returns>
        public ITradeStrategyGivenExpr<TName, TSource> Given(Func<IObservable<TSource>, IObservable<TSource>> observableFunc)
        {
            Context.Observable = observableFunc;
            return new TradeStrategyGivenExpr<TName, TSource>(Context);
        }
    }
}
