using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Framework.Messaging.Kafka;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference.Events;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.Event.Denormalizer.HostedService
{
    /// <summary>
    /// reference event denormalizer
    /// </summary>
    public class ReferenceEventDenormalizerConsumer: KafkaEventConsumer, IReferenceEventDenormalizerConsumer
    {
        private readonly IEventDenormalizer _eventDenormalizer;
        private readonly Guid _siteId;

        /// <summary>
        /// reference event denormalizer constructor
        /// </summary>
        /// <param name="eventDenormalizer"></param>
        /// <param name="options"></param>
        /// <param name="logger"></param>
        public ReferenceEventDenormalizerConsumer(IEventDenormalizer eventDenormalizer, IEventConsumerOptions options, ILogger<ReferenceEventDenormalizerConsumer> logger) : base(options, logger)
        {
            _eventDenormalizer = eventDenormalizer;
            _siteId = Guid.NewGuid();
        }

        /// <summary>
        /// consume selected reference events from event broker and execute denormalizer action for consumed event
        /// </summary>
        protected override void ConnectEvents()
        {
            var @events = new List<IEvent>
            {
                new LookupTypeCreatedEvent{ },
                new LookupTypeDeletedEvent{ },
                new EconomicCalendarAddedEvent{ },
                new EconomicCalendarRemovedEvent{ },
                new EconomicCalendarChangedEvent{ }
            };
            @events.ForEach(e => e.SetEventSource($"{EventTopic.ReferenceEvents}"));
            Subscribe(_siteId.ToString(), @events, async e => await _eventDenormalizer.DenormalizeEventAsync(e));
        }
    }
}
