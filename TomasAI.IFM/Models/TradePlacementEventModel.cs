using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.EventConsumer;

namespace TomasAI.IFM.Models
{
    public class TradePlacementEventModel : BaseModel<TradePlacementEventModel>
    {
        readonly ITradePlacementUIEventConsumer _tradePlacementEventConsumer;

        public TradePlacementEventModel(ITradePlacementUIEventConsumer tradePlacementEventConsumer)
        {
            _tradePlacementEventConsumer = tradePlacementEventConsumer ?? throw new ArgumentNullException(nameof(tradePlacementEventConsumer));
        }

        /// <summary>
        /// start listening for trade placement notification events
        /// </summary>
        /// <param name="listenerAction"></param>
        public async Task StartTradePlacementListenerAsync( Action<IEvent> listenerAction) 
            => await ExecuteAsync( () => _tradePlacementEventConsumer.StartAsync(listenerAction));

        /// <summary>
        /// stop listening for trade placement notification events
        /// </summary>
        public async Task StopTradePlacementListenerAsync() 
            => await ExecuteAsync( _tradePlacementEventConsumer.StopAsync );
        
    }
}
