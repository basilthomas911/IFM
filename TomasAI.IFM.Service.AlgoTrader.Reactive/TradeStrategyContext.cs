using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reactive.Subjects;
using TomasAI.IFM.Shared.AlgoTrader.Model;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Service.AlgoTrader.Reactive
{
    public class TradeStrategyContext<TName, TSource> where TName : ITradeStrategyType where TSource : TradePlan
    {

        public TradeStrategyContext(ISubject<TSource> subject, IEventProducer eventProducer)
        {
            Subject = subject;
            EventProducer = eventProducer;
        }
        public ITradeStrategy<TName, TSource> Root { get; set; }
        public ISubject<TSource> Subject { get; }
        public IEventProducer EventProducer { get; }
        public ITradeStrategyRuleInfo RuleInfo { get; set; }
        public Func<IObservable<TSource>, IObservable<TSource>> Observable { get; set; }
        public Action<TSource> Output { get; set; }
        public IDisposable Completed { get; set; }
    }
}
