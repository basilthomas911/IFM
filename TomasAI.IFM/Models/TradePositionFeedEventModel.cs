using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.EventConsumer;

namespace TomasAI.IFM.Models
{
    public class TradePositionFeedEventModel : BaseModel<TradePositionFeedEventModel>
    {
        readonly ITradePositionUIEventConsumer _tradePositionEventConsumer;

        public TradePositionFeedEventModel(ITradePositionUIEventConsumer tradePositionEventConsumer)
        {
            _tradePositionEventConsumer = tradePositionEventConsumer ?? throw new ArgumentNullException(nameof(tradePositionEventConsumer));
        }

        /// <summary>
        /// start listening for trade position updates
        /// </summary>
        /// <param name="listenerAction"></param>
        public async Task StartTradePositionListenerAsync(Action<TradePositionUpdatedEvent> listenerAction) 
            => await ExecuteAsync( () => _tradePositionEventConsumer.StartAsync(listenerAction) );

        /// <summary>
        /// stop listening for trade position updates
        /// </summary>
        public async Task StopTradePositionListenerAsync() 
            => await ExecuteAsync( _tradePositionEventConsumer.StopAsync );

        /// <summary>
        /// clear event queue;
        /// </summary>
        /// <returns></returns>
        public async Task ClearEventQueueAsync() 
            => await ExecuteAsync( _tradePositionEventConsumer.ClearEventQueueAsync );

    }
}
