using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.UI.EventConsumer;

namespace TomasAI.IFM.Models
{
    public class MarketDataEventModel : BaseModel<MarketDataEventModel>
    {
        readonly IMarketDataUIEventConsumer _eventConsumer;

        public MarketDataEventModel(IMarketDataUIEventConsumer marketDataEventConsumer)
        {
            _eventConsumer = IsArgumentNull.Set(marketDataEventConsumer);
        }

        /// <summary>
        /// start listening for market data notification events
        /// </summary>
        /// <param name="consumeEvents"></param>
        /// <param name="listenerAction"></param>
        public async ValueTask StartMarketDataListenerAsync(ICollection<IEvent> consumeEvents, Func<IEvent, ValueTask> listenerAction) 
            => await ExecuteValueTaskAsync( () => _eventConsumer.StartAsync(consumeEvents, listenerAction));

        /// <summary>
        /// stop listening for market data notification events
        /// </summary>
        public async ValueTask StopMarketDataListenerAsync() 
            => await ExecuteValueTaskAsync( _eventConsumer.StopAsync );
        
    }
}
