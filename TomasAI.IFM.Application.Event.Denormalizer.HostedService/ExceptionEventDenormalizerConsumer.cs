using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Framework.Messaging.Kafka;
using TomasAI.IFM.Shared.EventSourcing;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.Event.Denormalizer.HostedService
{
    /// <summary>
    /// exception event denormalizer
    /// </summary>
    public class ExceptionEventDenormalizerConsumer: KafkaEventConsumer, IExceptionEventDenormalizerConsumer
    {
        private readonly IEventDenormalizer _eventDenormalizer;
        private readonly Guid _siteId;

        /// <summary>
        /// exception event denormalizer constructor
        /// </summary>
        /// <param name="eventDenormalizer"></param>
        /// <param name="options"></param>
        /// <param name="logger"></param>
        public ExceptionEventDenormalizerConsumer(
            IEventDenormalizer eventDenormalizer, 
            IEventConsumerOptions options, 
            ILogger<ExceptionEventDenormalizerConsumer> logger) : base(options, logger)
        {
            _eventDenormalizer = eventDenormalizer;
            _siteId = Guid.NewGuid();
        }

        /// <summary>
        /// denormalize only selected exception events from event broker
        /// </summary>
        protected override void ConnectEvents()
        {
            var @events = new List<IEvent>
            {
                new CommandExceptionEvent{ },
                new QueryExceptionEvent{ },
                new DenormalizerExceptionEvent{ },
                new EventServiceExceptionEvent{ },
            };
            @events.ForEach(e => e.SetEventSource($"{EventTopic.ExceptionEvents}"));
            Subscribe(_siteId.ToString(), @events, async e => await _eventDenormalizer.DenormalizeEventAsync(e));
        }
    }
}
