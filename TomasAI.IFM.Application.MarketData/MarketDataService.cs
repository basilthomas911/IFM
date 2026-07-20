using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventService;

namespace TomasAI.IFM.Application.MarketData
{
    public class MarketDataService : BaseEventService, IMarketDataService
    {

        /// <summary>
        /// create market data service to execute api events via external event handlers
        /// </summary>
        /// <param name="eventHandlerResolver"></param>
        /// <param name="logger"></param>
        public MarketDataService(
            IEventServiceHandlerResolver eventHandlerResolver,
            ILogger<MarketDataService> logger):base( eventHandlerResolver, logger)
        {
        }

        protected override string ServiceName => GetType().Name;
    }
}
