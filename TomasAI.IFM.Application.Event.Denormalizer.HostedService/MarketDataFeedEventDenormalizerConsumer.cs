using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Framework.Messaging.Kafka;
using TomasAI.IFM.Shared.Extensions;

using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.Event.Denormalizer.HostedService
{
    /// <summary>
    /// market data feed event denormalizer consumer
    /// </summary>
    public class MarketDataFeedEventDenormalizerConsumer: KafkaEventConsumer, IMarketDataFeedEventDenormalizerConsumer
    {
        private readonly IEventDenormalizer _eventDenormalizer;
        private readonly Guid _siteId;

        /// <summary>
        /// construct market data feed event denormalizer consumer
        /// </summary>
        /// <param name="eventDenormalizer"></param>
        /// <param name="options"></param>
        /// <param name="logger"></param>
        public MarketDataFeedEventDenormalizerConsumer(
            IEventDenormalizer eventDenormalizer, 
            IEventConsumerOptions options, 
            ILogger<MarketDataFeedEventDenormalizerConsumer> logger) : base(options, logger)
        {
            _eventDenormalizer = eventDenormalizer;
            _siteId = Guid.NewGuid();
        }

        /// <summary>
        /// only consume market data feed events
        /// </summary>
        protected override void ConnectEvents()
        {
            var denormalizerEvents = new List<IEvent> {
                new FuturesTickDataInsertedEvent{  },
                new FuturesOptionTickDataInsertedEvent{ },
                new FuturesEodDataInsertedEvent{ },
                new VixFuturesEodDataInsertedEvent{ }
            };
            denormalizerEvents.ForEach(e => e.SetEventSource($"{EventTopic.MarketDataFeedEvents}"));
            Subscribe(_siteId.ToString(), denormalizerEvents, async e => await _eventDenormalizer.DenormalizeEventAsync(e));
        }
    }
}
