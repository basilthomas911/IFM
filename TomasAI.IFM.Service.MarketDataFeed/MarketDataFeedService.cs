using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventService;

namespace TomasAI.IFM.Service.MarketDataFeed
{
    public class MarketDataFeedService : BaseEventService, IMarketDataFeedService
    {

        /// <summary>
        /// create market data feed service to execute events via external event handlers
        /// </summary>
        /// <param name="eventHandlerResolver"></param>
        /// <param name="logger"></param>
        public MarketDataFeedService(
            IEventServiceHandlerResolver eventHandlerResolver,
            ILogger<MarketDataFeedService> logger):base(eventHandlerResolver, logger)
        {
        }

        protected override string ServiceName => GetType().Name;

    }
}
