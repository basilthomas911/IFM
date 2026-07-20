using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reactive;
using TomasAI.IFM.Shared.AlgoTrader;
using TomasAI.IFM.Shared.AlgoTrader.Model;

namespace TomasAI.IFM.Service.AlgoTrader.Reactive
{
    public interface ITradeStrategy<TName, TSource> : IObserver<TSource> where TName : ITradeStrategyType where TSource : TradePlan
    {
        /// <summary>
        /// return named rule expression
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        ITradeStrategyRuleExpr<TName, TSource> Rule(ITradeStrategyRuleInfo strategyRuleInfo);

        /// <summary>
        ///  dispose of all resources used for each strategy rule
        /// </summary>
        void Stop();

    }

    public interface ITradeStrategyRuleExpr<TName, TSource> where TName : ITradeStrategyType where TSource : TradePlan
    {
        TradeStrategyContext<TName, TSource> Context { get; }

        /// <summary>
        /// set lambda function to return an observable from input observable using reactive linq
        /// </summary>
        /// <param name="getRxObservable"></param>
        /// <returns></returns>
        ITradeStrategyGivenExpr<TName, TSource> Given(Func<IObservable<TSource>, IObservable<TSource>> getRxObservable);
    }

    public interface ITradeStrategyGivenExpr<TName, TSource> where TName : ITradeStrategyType where TSource : TradePlan
    {
        TradeStrategyContext<TName, TSource> Context { get; }
        ITradeStrategyOutputExpr<TName, TSource> Output(Action<TSource> setTradePlan);
    }

    public interface ITradeStrategyOutputExpr<TName, TSource> where TName : ITradeStrategyType where TSource : TradePlan
    {
        TradeStrategyContext<TName, TSource> Context { get; }
        ITradeStrategy<TName, TSource> As<TUpdate>( Func<TSource, TUpdate> outputUpdatedEvent) where TUpdate : ITradePlanUpdatedEvent;
    }

}
