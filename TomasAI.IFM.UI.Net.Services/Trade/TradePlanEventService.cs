using System;
using System.Threading.Tasks;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.UI.EventConsumer;

namespace TomasAI.IFM.UI.Net.Services.Trade
{
    /// <summary>Provides the TradePlanEventService UI service boundary.</summary>
    public class TradePlanEventService : UiServiceBase<TradePlanEventService>
    {
        readonly ITradePlanUIEventConsumer _tradePlanEventConsumer;

        /// <summary>Executes or exposes a documented UI service operation.</summary>
        public TradePlanEventService(ITradePlanUIEventConsumer tradePlanEventConsumer)
        {
            _tradePlanEventConsumer = tradePlanEventConsumer ?? throw new ArgumentNullException(nameof(tradePlanEventConsumer));
        }

        /// <summary>
        /// start listening for trade plan updated events
        /// </summary>
        /// <param name="listenerAction"></param>
        public async Task StartTradePlanListenerAsync(Func<TradePlanUpdatedEvent, ValueTask> listenerAction)
            => await ExecuteValueTaskAsync( () => _tradePlanEventConsumer.StartAsync(listenerAction));

        /// <summary>
        /// stop listening for trade plan updated events
        /// </summary>
        public async Task StopTradePlanListenerAsync() 
            => await ExecuteValueTaskAsync( _tradePlanEventConsumer.StopAsync );
        
    }
}
