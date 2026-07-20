using System;
using System.Threading.Tasks;
using TomasAI.IFM.KafkaClient;
using TomasAI.IFM.Shared.Kafka;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Fund.Events;
using TomasAI.IFM.Shared.MarketData.Events;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.OptionPricer.Events;
using TomasAI.IFM.Shared.Reference.Events;
using TomasAI.IFM.Shared.Trade.Events;

namespace TomasAI.IFM.Application.Event.Denormalizer.EventProducer
{
    public class EventDenormalizerEventProducer : KafkaEventProducer, IEventDenormalizerEventProducer
    {
        public EventDenormalizerEventProducer(IEventProducerOptions options):base(options)
        {
        }

        public async Task ExecuteAsync(FundEventDenormalizerCompletedEvent e) => await ProduceAsync<Guid, FundEventDenormalizerCompletedEvent>(e.CommandId, e);
        public async Task ExecuteAsync(FundEventDenormalizerFailedEvent e) => await ProduceAsync<Guid, FundEventDenormalizerFailedEvent>(e.CommandId, e);
        public async Task ExecuteAsync(MarketDataEventDenormalizerCompletedEvent e) => await ProduceAsync<Guid, MarketDataEventDenormalizerCompletedEvent>(e.CommandId, e);
        public async Task ExecuteAsync(MarketDataEventDenormalizerFailedEvent e) => await ProduceAsync<Guid, MarketDataEventDenormalizerFailedEvent>(e.CommandId, e);
        public async Task ExecuteAsync(MarketDataFeedEventDenormalizerCompletedEvent e) => await ProduceAsync<Guid, MarketDataFeedEventDenormalizerCompletedEvent>(e.CommandId, e);
        public async Task ExecuteAsync(MarketDataFeedEventDenormalizerFailedEvent e) => await ProduceAsync<Guid, MarketDataFeedEventDenormalizerFailedEvent>(e.CommandId, e);
        public async Task ExecuteAsync(OptionPricerEventDenormalizerCompletedEvent e) => await ProduceAsync<Guid, OptionPricerEventDenormalizerCompletedEvent>(e.CommandId, e);
        public async Task ExecuteAsync(OptionPricerEventDenormalizerFailedEvent e) => await ProduceAsync<Guid, OptionPricerEventDenormalizerFailedEvent>(e.CommandId, e);
        public async Task ExecuteAsync(ReferenceEventDenormalizerCompletedEvent e) => await ProduceAsync<Guid, ReferenceEventDenormalizerCompletedEvent>(e.CommandId, e);
        public async Task ExecuteAsync(ReferenceEventDenormalizerFailedEvent e) => await ProduceAsync<Guid, ReferenceEventDenormalizerFailedEvent>(e.CommandId, e);
        public async Task ExecuteAsync(TradeEventDenormalizerCompletedEvent e) => await ProduceAsync<Guid, TradeEventDenormalizerCompletedEvent>(e.CommandId, e);
        public async Task ExecuteAsync(TradeEventDenormalizerFailedEvent e) => await ProduceAsync<Guid, TradeEventDenormalizerFailedEvent>(e.CommandId, e);

        public async Task ExecuteAsync(TradeAddedToFundOrderCompleteEvent e) => await ProduceAsync<Guid, TradeAddedToFundOrderCompleteEvent>(e.CommandId, e);
    }
}
