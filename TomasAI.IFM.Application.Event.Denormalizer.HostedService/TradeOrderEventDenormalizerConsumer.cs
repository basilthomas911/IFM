using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Framework.Messaging.Kafka;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.Events;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.Event.Denormalizer.HostedService
{
    /// <summary>
    /// trade order event denormalizer
    /// </summary>
    public class TradeOrderEventDenormalizerConsumer: KafkaEventConsumer, ITradeOrderEventDenormalizerConsumer
    {
        private readonly IEventDenormalizer _eventDenormalizer;
        private readonly Guid _siteId;
 
        /// <summary>
        /// trade order event denormalizer constructor
        /// </summary>
        /// <param name="eventDenormalizer"></param>
        /// <param name="options"></param>
        /// <param name="logger"></param>
        public TradeOrderEventDenormalizerConsumer(IEventDenormalizer eventDenormalizer, IEventConsumerOptions options, ILogger<TradeEventDenormalizerConsumer> logger) : base(options, logger)
        {
            _eventDenormalizer = eventDenormalizer;
            _siteId = Guid.NewGuid();
        }

        /// <summary>
        /// consume trade order events from event broker and execute denormalizer action for consumed event
        /// </summary>
        protected override void ConnectEvents()
        {
            var @events = new List<IEvent> {
               new OrderPlacedEvent{ },
               new OrderFilledEvent{ },
               new OrderCancelledEvent{ },
               new OrderUpdatedEvent{ },
               new OrderExecutedEvent{ }
            };
            events.ForEach(e => e.SetEventSource($"{EventTopic.TradeOrderEvents}"));
            Subscribe(_siteId.ToString(), events, async e => await _eventDenormalizer.DenormalizeEventAsync(e));
        }
    }
}
