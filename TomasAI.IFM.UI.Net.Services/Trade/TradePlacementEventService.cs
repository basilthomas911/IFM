using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.EventConsumer;

namespace TomasAI.IFM.UI.Net.Services.Trade
{
    /// <summary>Provides the TradePlacementEventService UI service boundary.</summary>
    public class TradePlacementEventService : UiServiceBase<TradePlacementEventService>
    {
        readonly ITradePlacementUIEventConsumer _tradePlacementEventConsumer;

        /// <summary>Executes or exposes a documented UI service operation.</summary>
        public TradePlacementEventService(ITradePlacementUIEventConsumer tradePlacementEventConsumer)
        {
            _tradePlacementEventConsumer = tradePlacementEventConsumer ?? throw new ArgumentNullException(nameof(tradePlacementEventConsumer));
        }

        /// <summary>
        /// start listening for trade placement notification events
        /// </summary>
        /// <param name="listenerAction"></param>
        public async Task StartTradePlacementListenerAsync(Func<IEvent, ValueTask> listenerAction)
            => await ExecuteValueTaskAsync( () => _tradePlacementEventConsumer.StartAsync(listenerAction));

        /// <summary>
        /// stop listening for trade placement notification events
        /// </summary>
        public async Task StopTradePlacementListenerAsync() 
            => await ExecuteValueTaskAsync( _tradePlacementEventConsumer.StopAsync );
        
    }
}
