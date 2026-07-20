using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.AlgoTrader;
using TomasAI.IFM.Shared.AlgoTrader.Model;

namespace TomasAI.IFM.Service.AlgoTrader.Reactive
{
    /// <summary>
    /// strategy output expression
    /// </summary>
    /// <typeparam name="TName"></typeparam>
    /// <typeparam name="TSource"></typeparam>
    public class TradeStrategyOutputExpr<TName, TSource> : ITradeStrategyOutputExpr<TName, TSource> where TName : ITradeStrategyType where TSource : TradePlan
    {
        /// <summary>
        /// strategy output expression constructor
        /// </summary>
        /// <param name="context"></param>
        public TradeStrategyOutputExpr(TradeStrategyContext<TName, TSource> context)
        {
            Context = context;
        }

        /// <summary>
        /// strategy execution context
        /// </summary>
        public TradeStrategyContext<TName, TSource> Context { get; }

        /// <summary>
        /// build stratey rule by connecting observable to event consumer and subsribe observable to output to event producer
        /// </summary>
        /// <param name="updateTradePlan">strategy output expression</param>
        /// <returns></returns>
        public ITradeStrategy<TName, TSource> As<TUpdate>(Func<TSource, TUpdate> getUpdatedEvent) where TUpdate : ITradePlanUpdatedEvent
        {
            // create strategy observable...
            var o = Context.Observable(Context.Subject);

            // create strategy subscriber...
            Context.Completed = o.Subscribe(async tp => await OnSubscribeHandlerAsync(tp));
            return Context.Root;

            async Task OnSubscribeHandlerAsync(TSource tradePlan)
            {
                // update trade plan...
                Context.Output(tradePlan);

                // get updated event...
                var updatedEvent = getUpdatedEvent(tradePlan);

                // post updated event to event broker...
                await Context.EventProducer.PostEventAsync(updatedEvent);
            }
        }
    }
}
