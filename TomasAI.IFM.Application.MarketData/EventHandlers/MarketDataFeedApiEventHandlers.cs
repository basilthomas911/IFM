using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventService;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.MarketDataFeed.Events.Api;
using TomasAI.IFM.Shared.Log.ServiceApi;

namespace TomasAI.IFM.Application.MarketData.EventHandlers
{
    public class MarketDataFeedApiEventHandlers : BaseEventServiceHandler,
        IAsyncEventHandler<MarketDataFeedStartedApiEvent, MarketDataService>
    {
        IMarketDataApi MarketDataApi { get; }
        IMarketDataApiEventProducer MarketDataApiEventProducer { get; }

        public MarketDataFeedApiEventHandlers(
            IMarketDataApi marketDataApi,
            IMarketDataApiEventProducer marketDataApiEventProducer,
            IStatusConsoleWriter statusConsoleWriter) : base(statusConsoleWriter)
        {
            ArgumentNullException.ThrowIfNull(marketDataApi);
            ArgumentNullException.ThrowIfNull(marketDataApiEventProducer);
            MarketDataApi = marketDataApi;
            MarketDataApiEventProducer = marketDataApiEventProducer;
        }

        /// <summary>
        /// start market data feed
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(MarketDataFeedStartedApiEvent e)
        {
            try
            {
                MarketDataApi.Start();  
                await MarketDataApiEventProducer.PostEventAsync(e.ToCompletedEvent());
            }
            catch (Exception ex)
            {
                
                await MarketDataApiEventProducer.PostEventAsync(e.ToFailedEvent(ex));
            }
        }

    }
}
