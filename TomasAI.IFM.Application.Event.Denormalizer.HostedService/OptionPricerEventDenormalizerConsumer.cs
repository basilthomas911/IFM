using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Framework.Messaging.Kafka;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.OptionPricer.Events;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.Event.Denormalizer.HostedService
{
    /// <summary>
    /// option pricer event denormalizer consumer
    /// </summary>
    public class OptionPricerEventDenormalizerConsumer: KafkaEventConsumer, IOptionPricerEventDenormalizerConsumer
    {
        private readonly IEventDenormalizer _eventDenormalizer;
        private readonly Guid _siteId;

        /// <summary>
        /// option pricer event denormalizer consumer constructor
        /// </summary>
        /// <param name="eventDenormalizer"></param>
        /// <param name="options"></param>
        /// <param name="logger"></param>
        public OptionPricerEventDenormalizerConsumer(IEventDenormalizer eventDenormalizer, IEventConsumerOptions options, ILogger<OptionPricerEventDenormalizerConsumer> logger) : base(options, logger)
        {
            _eventDenormalizer = eventDenormalizer;
            _siteId = Guid.NewGuid(); 
        }

        /// <summary>
        /// denormalize option pricer events from event broker
        /// </summary>
        protected override void ConnectEvents()
        {
            var @events = new List<IEvent>
            {
                new SpreadDistributionInsertedEvent{ },
                new SpreadDistributionJobCreatedEvent{ },
                new SpreadDistributionJobSucceededEvent{ },
                new SpreadDistributionJobFaultedEvent{ }
            };
            @events.ForEach(e => e.SetEventSource($"{EventTopic.OptionPricerEvents}"));
            Subscribe(_siteId.ToString(), @events, async e => await _eventDenormalizer.DenormalizeEventAsync(e));
        }
    }
}
