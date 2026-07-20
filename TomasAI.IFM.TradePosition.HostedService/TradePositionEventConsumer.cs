using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Framework.Messaging.Kafka;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Service.TradePosition.HostedService
{
    /// <summary>
    /// trade position event consumer
    /// </summary>
    public class TradePositionEventConsumer : KafkaEventConsumer, ITradePositionEventConsumer
    {
        private readonly ITradePositionService _tradePositionService;
        private readonly Guid _siteId;

        /// <summary>
        /// trade position event consumer constructor
        /// </summary>
        /// <param name="tradePositionService"></param>
        /// <param name="options"></param>
        /// <param name="logger"></param>
        public TradePositionEventConsumer(ITradePositionService tradePositionService, IEventConsumerOptions options, ILogger<TradePositionEventConsumer> logger):base(options, logger)
        {
            _tradePositionService = tradePositionService;
            _siteId = Guid.NewGuid();
        }

        /// <summary>
        /// consume only trade position events from event broker
        /// </summary>
        protected override void ConnectEvents()
        {
            var @events = new List<IEvent>
            {
                new TradePositionChangedEvent{ },
                new OptionTradeLegDataChangedEvent{ },
                new OptionTradeEndOfDayProcessedEvent{ },
                new OptionTradeDistributionStatisticsChangedEvent{ },
                new OptionTradeOrderPlacedEvent{ },
                new OptionTradePositionOpenedEvent{ },
                new OptionTradePositionClosedEvent{ },
            };
            @events.ForEach(e => e.SetEventSource($"{EventTopic.TradeEvents}"));
            Subscribe($"{_siteId}", @events, async e => await _tradePositionService.ExecuteAsync((dynamic )e));
        }
    }
}
