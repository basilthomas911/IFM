using System;
using System.Threading.Tasks;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.UI.EventConsumer;

namespace TomasAI.IFM.UI.Net.Models
{
    public class TradePlanEventModel : BaseModel<TradePlanEventModel>
    {
        readonly ITradePlanUIEventConsumer _tradePlanEventConsumer;

        public TradePlanEventModel(ITradePlanUIEventConsumer tradePlanEventConsumer)
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
