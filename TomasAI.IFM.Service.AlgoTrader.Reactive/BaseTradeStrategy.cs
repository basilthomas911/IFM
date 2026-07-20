using System;
using System.Collections.Immutable;
using System.Reactive.Subjects;
using System.Reactive.Linq;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.AlgoTrader.Model;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Service.AlgoTrader.Reactive
{
    /// <summary>
    /// trade strategy
    /// </summary>
    public class BaseTradeStrategy<TName, TSource> : ITradeStrategy<TName, TSource> where TName: ITradeStrategyType where TSource:TradePlan
    {
        private readonly ISubject<TSource> _subject;
        private readonly IEventProducer _eventProducer;
        private readonly ImmutableList<TradeStrategyContext<TName, TSource>> _strategyContexts;

        /// <summary>
        /// trade plan observable constructor
        /// </summary>
        public BaseTradeStrategy(IEventProducer eventProducer)
        {
            _subject = new Subject<TSource>();
            _eventProducer = eventProducer;
            _strategyContexts = ImmutableList.Create<TradeStrategyContext<TName, TSource>>();
        }

        public void OnNext(TSource value) => _subject.OnNext(value);

        public void OnCompleted() => _subject.OnCompleted();

        public void OnError(Exception error) => _subject.OnError(error);

        /// <summary>
        /// create new strategy rule
        /// </summary>
        /// <param name="ruleInfo"></param>
        /// <returns></returns>
        public ITradeStrategyRuleExpr<TName, TSource> Rule(ITradeStrategyRuleInfo ruleInfo)
        {
            // create new context for newly created trade strategy rule...
            var context = new TradeStrategyContext<TName, TSource>(_subject, _eventProducer);
            context.Root = this;
            context.RuleInfo = ruleInfo;
            _strategyContexts.Add(context);
            return new TradeStrategyRuleExpr<TName, TSource>(context);
        }

        public void Stop()
        {
            foreach (var e in _strategyContexts)
                e.Completed.Dispose();
        }
    }
}
