using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Framework.Messaging.Kafka;
using TomasAI.IFM.Shared.MarketDataFeed.Events.Api;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.MarketData.HostedService
{
    public class MarketDataApiEventConsumer : KafkaEventConsumer, IMarketDataApiEventConsumer
    {
        readonly IMarketDataService _marketDataService;
        readonly Guid _siteId;

        /// <summary>
        /// market data api event consumer constructor
        /// </summary>
        /// <param name="marketDataService"></param>
        /// <param name="options"></param>
        /// <param name="logger"></param>
        public MarketDataApiEventConsumer(
            IMarketDataService marketDataService, 
            IEventConsumerOptions options, 
            ILogger logger) :base(options, logger)
        {
            _marketDataService = marketDataService ?? throw new ArgumentNullException(nameof(marketDataService));
            _siteId = Guid.NewGuid();
        }

        /// <summary>
        /// execute event handler for market data api events from event broker
        /// </summary>
        protected override void ConnectEvents()
        {
            var @events = new List<IEvent>
            {
                new MarketDataFeedStartedApiEvent{ },
            };
            @events.ForEach(e => e.SetEventSource($"{EventTopic.MarketDataApiEvents}"));
            Subscribe($"{_siteId}", @events,  async e => await _marketDataService.ExecuteAsync(e));
        }

    }
}
