using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using TomasAI.IFM.Application.Query.SignalRClient;
using TomasAI.IFM.Application.Query.SignalRClient.EventDenormalizers;

using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.Log;
using TomasAI.IFM.Shared.Log.ServiceApi;
using TomasAI.IFM.Shared.Trade.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Event
{
    public class MarketDataFeedEvents : BaseEvents,
        IAsyncEventHandler<MarketDataFeedStartedEvent>,
        IAsyncEventHandler<MarketDataFeedStoppedEvent>,
        IAsyncEventHandler<MarketDataFeedResetEvent>,
        IAsyncEventHandler<MarketDataFeedResetCompletedEvent>,
        IAsyncEventHandler<FuturesTickDataStreamingStartedEvent>,
        IAsyncEventHandler<FuturesTickDataInsertedEvent>,
        IAsyncEventHandler<FuturesEodDataInsertedEvent>,
        IAsyncEventHandler<FuturesTickDataStreamingStoppedEvent>,
        IAsyncEventHandler<FuturesOptionTickDataStreamingStartedEvent>,
        IAsyncEventHandler<FuturesOptionTickDataInsertedEvent>,
        IAsyncEventHandler<FuturesOptionTickDataStreamingStoppedEvent>,
        IAsyncEventHandler<TradeLiveFeedAddedEvent>,
        IAsyncEventHandler<TradeLiveFeedRemovedEvent>,
        IAsyncEventHandler<TradeLiveFeedsRemovedEvent>,
        IAsyncEventHandler<TradeLiveFeedTurnedOnEvent>,
        IAsyncEventHandler<TradeLiveFeedTurnedOffEvent>
    {
        private const int ErrorCodeBase = 3000;
        private readonly IMarketDataFeedServiceApi _marketDataFeedServiceApi;
        private readonly IMarketDataFeedEventDenormalizerApi _eventDenormalizer;
        private readonly ITradeEventDenormalizerApi _tradeDenormalizer;
        private readonly IMarketDataFeedQueryApi _queryApi;
        private readonly IMarketDataFeedCommandApi _commandApi;
        private static FuturesEodDataViewModel _futuresEodDataCached;

        /// <summary>
        /// created event handlers for market data feed generated events
        /// </summary>
        /// <param name="dbTrade"></param>
        /// <param name="logger"></param>
        public MarketDataFeedEvents(IMarketDataFeedServiceApi marketDataFeedServiceApi, IMarketDataFeedEventDenormalizerApi eventDenormalizer, IMarketDataFeedQueryApi queryApi, IMarketDataFeedCommandApi commandApi,
            ITradeEventDenormalizerApi tradeDenormalizer, IStatusConsoleServiceApi statusConsoleLog) :base(statusConsoleLog)
        {
            _marketDataFeedServiceApi = marketDataFeedServiceApi;
            _eventDenormalizer = eventDenormalizer;
            _tradeDenormalizer = tradeDenormalizer;
            _queryApi = queryApi;
            _commandApi = commandApi;
        }

        /// <summary>
        /// execute handler when futures tick data inserted event is generated
        /// </summary>
        /// <param name="e">futures tick data inserted event</param>
        /// <returns></returns>
        public async Task ExecuteAsync(FuturesTickDataInsertedEvent e)
            => await ExecuteAsync(ErrorCodeBase, async () => {

                // save futures tick data...
                //await _eventDenormalizer.InsertFuturesTickDataAsync(e);

                // create futures eod data...
                var futuresTickData = e.TickData.ToList();
                var lastFuturesTickData = futuresTickData.Last();
                var valueDate = lastFuturesTickData.ValueDate;
                var serviceResult = await _queryApi.GetFuturesEodDataParametersAsync(e.Contract.ContractId, valueDate);
                if (serviceResult.Success)
                {
                    var feodParams = serviceResult.Value;
                    var eodDataToday = feodParams.FuturesEodDataToday;
                    var eodDataRange = feodParams.FuturesEodDataRange.OrderByDescending(o => o.ValueDate).ToList();
                    var normCurveTbl = new NormalCurveTableReadModel { NormalCurveTable = feodParams.NormalCurveTable.NormalCurveTable };

                    // save futures eod data...
                    await _commandApi.InsertFuturesEodDataAsync(valueDate, lastFuturesTickData, e.Contract, eodDataToday, eodDataRange, normCurveTbl, 20);
                }
            });

        /// <summary>
        /// execute handler when futures eod data inserted event is generated
        /// </summary>
        /// <param name="e">futures eod data inserted event</param>
        /// <returns></returns>
        public async Task ExecuteAsync(FuturesEodDataInsertedEvent e) 
            => await ExecuteAsync(ErrorCodeBase + 1, async () => {
                //await _eventDenormalizer.InsertFuturesEodDataAsync(e);
                // post updated futures eod data to any listeners...
                if (_futuresEodDataCached == null || Convert.ToDecimal(_futuresEodDataCached.ClosePrice) != Convert.ToDecimal(e.FuturesEodData.ClosePrice))
                {
                    _futuresEodDataCached = e.FuturesEodData;
                    await _marketDataFeedServiceApi.FuturesEodDataUpdatedAsync(new FuturesEodDataUpdatedEvent { FuturesEodData = e.FuturesEodData });
                }
            });

        /// <summary>
        /// execute handler when futures option tick data inserted event is generated
        /// </summary>
        /// <param name="e">futures option tick data inserted event</param>
        /// <returns></returns>
        public async Task ExecuteAsync(FuturesOptionTickDataInsertedEvent e)
            => await ExecuteAsync(ErrorCodeBase + 1, async () => {
                await _eventDenormalizer.ExecuteAsync(e);
                await _marketDataFeedServiceApi.FuturesOptionTickDataUpdatedAsync(new FuturesOptionTickDataUpdatedEvent { OptionTickData = e.TickData });
            });
        

        /// <summary>
        /// execute handler when market data feed starts
        /// </summary>
        /// <param name="e">MarketDataFeedStartedEvent</param>
        /// <returns></returns>
        public async Task ExecuteAsync(MarketDataFeedStartedEvent e) => await ExecuteAsync(ErrorCodeBase+2, () => _marketDataFeedServiceApi.MarketDataFeedStartedAsync(e));

        /// <summary>
        /// execute handler when market data feed stops
        /// </summary>
        /// <param name="e">MarketDataFeedStoppedEvent</param>
        /// <returns></returns>
        public async Task ExecuteAsync(MarketDataFeedStoppedEvent e) => await ExecuteAsync(ErrorCodeBase+3, () => _marketDataFeedServiceApi.MarketDataFeedStoppedAsync(e));

        /// <summary>
        /// execute handler when market data feed resets
        /// </summary>
        /// <param name="e">MarketDataFeedResetEvent</param>
        /// <returns></returns>
        public async Task ExecuteAsync(MarketDataFeedResetEvent e) => await ExecuteAsync(ErrorCodeBase + 4, () => _marketDataFeedServiceApi.MarketDataFeedResetAsync(e));
        public async Task ExecuteAsync(MarketDataFeedResetCompletedEvent e) => await ExecuteAsync(ErrorCodeBase + 5, () => _marketDataFeedServiceApi.MarketDataFeedResetCompletedAsync(e));

        public async Task ExecuteAsync(FuturesTickDataStreamingStartedEvent e) => await ExecuteAsync(ErrorCodeBase + 6, () => _marketDataFeedServiceApi.FuturesTickDataStreamingStartedAsync(e));

        public async Task ExecuteAsync(FuturesTickDataStreamingStoppedEvent e) => await ExecuteAsync(ErrorCodeBase + 7, () => _marketDataFeedServiceApi.FuturesTickDataStreamingStoppedAsync(e));

        public async Task ExecuteAsync(FuturesOptionTickDataStreamingStartedEvent e) => await ExecuteAsync(ErrorCodeBase + 8, () => _marketDataFeedServiceApi.FuturesOptionTickDataStreamingStartedAsync(e));

        public async Task ExecuteAsync(FuturesOptionTickDataStreamingStoppedEvent e) => await ExecuteAsync(ErrorCodeBase + 9, () => _marketDataFeedServiceApi.FuturesOptionTickDataStreamingStoppedAsync(e));

        /// <summary>
        /// add trade live feed
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(TradeLiveFeedAddedEvent e) => await ExecuteAsync(ErrorCodeBase+10, () => _tradeDenormalizer.AddTradeLiveFeedAsync(e));

        /// <summary>
        /// remove trade live feed
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(TradeLiveFeedRemovedEvent e) => await ExecuteAsync(ErrorCodeBase+11, () => _tradeDenormalizer.RemoveTradeLiveFeedAsync(e));

        /// <summary>
        /// remove all trade live feeds for this order
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(TradeLiveFeedsRemovedEvent e) => await ExecuteAsync(ErrorCodeBase + 12, () => _tradeDenormalizer.RemoveTradeLiveFeedsAsync(e));

        /// <summary>
        /// turn trade live feed on
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(TradeLiveFeedTurnedOnEvent e) => await ExecuteAsync(ErrorCodeBase+13, () => _tradeDenormalizer.TurnTradeLiveFeedOnAsync(e));

        /// <summary>
        /// turn trade live feed off
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(TradeLiveFeedTurnedOffEvent e) => await ExecuteAsync(ErrorCodeBase+14, () => _tradeDenormalizer.TurnTradeLiveFeedOffAsync(e));

        protected override async Task WriteConsoleAsync(string message) => await WriteConsoleAsync(StatusSourceType.MarketDataFeed, message);

        protected override async Task WriteConsoleAsync(Exception ex, int errorCode) => await WriteConsoleAsync(StatusSourceType.MarketDataFeed, ex, errorCode);

        
    }
}
