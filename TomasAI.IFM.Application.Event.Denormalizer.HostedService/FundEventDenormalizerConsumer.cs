using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Framework.Messaging.Kafka;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Fund.Events;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.Event.Denormalizer.HostedService
{
    /// <summary>
    /// fund event denormalizer consumer
    /// </summary>
    public class FundEventDenormalizerConsumer : KafkaEventConsumer, IFundEventDenormalizerConsumer
    {
        private readonly IEventDenormalizer _eventDenormalizer;
        private readonly Guid _siteId;

        /// <summary>
        /// fund event denormalizer consumer constructor
        /// </summary>
        /// <param name="eventDenormalizer"></param>
        /// <param name="options"></param>
        /// <param name="logger"></param>
        public FundEventDenormalizerConsumer(IEventDenormalizer eventDenormalizer, IEventConsumerOptions options, ILogger<FundEventDenormalizerConsumer> logger) :base(options, logger)
        {
            _eventDenormalizer = eventDenormalizer;
            _siteId = Guid.NewGuid();
        }

        /// <summary>
        /// denoralize fund events from event broker
        /// </summary>
        protected override void ConnectEvents()
        {
            var @events = new List<IEvent> { 
                new FundCreatedEvent{ },
                new FundTransactionsEvent{ },
                new OrderAddedToFundEvent{ },
                new OrderRemovedFromFundEvent{ },
                new TradeAddedToFundOrderEvent{ },
                new TradeRemovedFromFundOrderEvent{ },
                new FundOrderTradeStateChangedEvent{ },
                new OpeningTradeFundTransactionCreatedEvent{ },
                new OpeningTradeFundTransactionAdjustmentCreatedEvent{ },
                new TradeCommissionFundTransactionCreatedEvent{ },
                new RealizedTradePnlFundTransactionCreatedEvent{ },
                new UnrealizedTradePnlFundTransactionCreatedEvent{ },
                new TradeCommissionFundTransactionAdjustmentCreatedEvent{ },
                new UnrealizedTradePnlFundTransactionAdjustmentCreatedEvent{ },
                new RealizedTradePnlFundTransactionAdjustmentCreatedEvent{ },
                new FundOrderStatusChangedEvent{ },
                new EndOfDayFundTransactionProcessedEvent{ },
                new FundOrderClosedEvent{ }
            };
            events.ForEach(e => e.SetEventSource($"{EventTopic.FundEvents}"));
            Subscribe(_siteId.ToString(), @events, async e => await _eventDenormalizer.DenormalizeEventAsync(e));
        }
    }
}
