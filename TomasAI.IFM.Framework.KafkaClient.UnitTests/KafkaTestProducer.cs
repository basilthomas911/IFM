using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Framework.Messaging.Kafka;

namespace TomasAI.IFM.Framework.KafkaClient.UnitTests
{
    /// <summary>
    /// 
    /// </summary>
    public class KafkaTestProducer : KafkaEventProducer
    {
        public KafkaTestProducer(IEventProducerOptions options, ILogger logger):base(options, logger)
        {
        }

        public override Task PostEventAsync(IEvent @event)
        {
            throw new NotImplementedException();
        }

        public async Task ProduceAsync(string eventKey, MarketDataFeedStartedEvent eventValue) => await this.SendEventAsync<string, MarketDataFeedStartedEvent>(eventKey, eventValue);
        
    }
}
