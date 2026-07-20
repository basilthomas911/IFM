using System;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.UI.EventConsumer;

namespace TomasAI.IFM.Models
{
    public class MarketDataFeedEventModel : BaseModel<MarketDataFeedEventModel>
    {
        readonly IFuturesOptionQuoteDataUIEventConsumer _futuresOptionQuoteDataEventConsumer;

        public MarketDataFeedEventModel(IFuturesOptionQuoteDataUIEventConsumer futuresOptionQuoteDataEventConsumer)
        {
            _futuresOptionQuoteDataEventConsumer = IsArgumentNull.Set(futuresOptionQuoteDataEventConsumer);
        }

        /// <summary>
        /// start listening for futures option quote data notification events
        /// </summary>
        /// <param name="listenerAction"></param>
        public async Task StartFuturesOptionQuoteDataListenerAsync(Action<FuturesOptionQuoteDataUpdatedEvent> listenerAction) 
            => await ExecuteAsync( () => _futuresOptionQuoteDataEventConsumer.StartAsync( listenerAction));

        /// <summary>
        /// stop listening for futures option quote data notification events
        /// </summary>
        public async Task StopFuturesOptionQuoteDataListenerAsync() 
            => await ExecuteAsync(_futuresOptionQuoteDataEventConsumer.StopAsync );
        
    }
}
