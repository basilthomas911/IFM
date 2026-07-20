using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventService;

namespace TomasAI.IFM.Service.MarketDataAnalytics
{
    /// <summary>
    /// market data analytics service
    /// </summary>
    public class MarketDataAnalyticsService : BaseEventService, IMarketDataAnalyticsService
    {

        /// <summary>
        /// create market data analytics service to execute events via external event handlers
        /// </summary>
        /// <param name="eventHandlerResolver"></param>
        /// <param name="logger"></param>
        public MarketDataAnalyticsService(
            IEventServiceHandlerResolver eventHandlerResolver,
            ILogger<MarketDataAnalyticsService> logger):base(eventHandlerResolver, logger)
        {
        }

        protected override string ServiceName => GetType().Name;

    }
}
