using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Framework.Messaging.Kafka;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.Events;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.Event.Denormalizer.HostedService
{
    /// <summary>
    /// market data event denormalizer consumeer
    /// </summary>
    public class MarketDataEventDenormalizerConsumer: KafkaEventConsumer, IMarketDataEventDenormalizerConsumer
    {
        private readonly IEventDenormalizer _eventDenormalizer;
        private readonly Guid _siteId;

        /// <summary>
        /// market data event denormalizer consumeer constructor
        /// </summary>
        /// <param name="eventDenormalizer"></param>
        /// <param name="options"></param>
        /// <param name="logger"></param>
        public MarketDataEventDenormalizerConsumer(
            IEventDenormalizer eventDenormalizer, 
            IEventConsumerOptions options, 
            ILogger<MarketDataEventDenormalizerConsumer> logger) : base(options, logger)
        {
            _eventDenormalizer = eventDenormalizer;
            _siteId = Guid.NewGuid();
        }

        /// <summary>
        /// denormalize selected market data events from event broker
        /// </summary>
        protected override void ConnectEvents()
        {
            var @events = new List<IEvent>
            {
                new FuturesContractAddedEvent{ },
                new FuturesContractRemovedEvent{ },
                new FuturesContractChangedEvent{ },
                new FuturesOptionContractAddedEvent{ },
                new FuturesOptionContractRemovedEvent{ },
                new FuturesOptionContractChangedEvent{ },
                new YieldCurveRateAddedEvent{ },
                new YieldCurveRateChangedEvent{ },
                new YieldCurveRateRemovedEvent{ },
                new YieldCurveRatesImportedEvent{ }
            };
            events.ForEach(e => e.SetEventSource($"{EventTopic.MarketDataEvents}"));
            Subscribe(_siteId.ToString(), @events, async e => await _eventDenormalizer.DenormalizeEventAsync(e) );
        }
    }
}
