using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Event.Denormalizer
{
    public class MarketDataFeedEventDenormalizer : BaseEventDenormalizer,
        IAsyncEventHandler<FuturesTickDataInsertedEvent>,
        IAsyncEventHandler<FuturesOptionTickDataInsertedEvent>,
        IAsyncEventHandler<FuturesEodDataInsertedEvent>,
        IAsyncEventHandler<VixFuturesEodDataInsertedEvent>,
        IAsyncEventHandler<TradeLiveFeedAddedEvent>,
        IAsyncEventHandler<TradeLiveFeedRemovedEvent>
    {
        private const int Err_MarketDataFeedEventDenormalizer = 5003;
        private readonly IMarketDataDbContext _dbMarketData;
        private readonly IMarketDataFeedEventProducer _eventProducer;

        public MarketDataFeedEventDenormalizer(IDbContextFactory dbFactory, IMarketDataFeedEventProducer eventProducer, ILogger<MarketDataFeedEventDenormalizer> logger):base(logger)
        {
            _dbMarketData = dbFactory.MarketDataDb as IMarketDataDbContext;
            _eventProducer = eventProducer;
            SetEventProducer(e => _eventProducer.PostEventAsync((dynamic)e));
        }

        /// <summary>
        /// insert futures tick data into query data
        /// </summary>
        /// <param name="e">FuturesTickDataInsertedEvent</param>
        /// <returns></returns>
        public async Task ExecuteAsync(FuturesTickDataInsertedEvent e) => await DenormalizeAsync(e, Err_MarketDataFeedEventDenormalizer, () => _dbMarketData.DbWriter.InsertFuturesTickDataAsync(e.TickData));

        /// <summary>
        /// insert futures option tick data into query store
        /// </summary>
        /// <param name="e">FuturesOptionTickDataInsertedEvent</param>
        /// <returns></returns>
        public async Task ExecuteAsync(FuturesOptionTickDataInsertedEvent e) => await DenormalizeAsync(e, Err_MarketDataFeedEventDenormalizer, () => _dbMarketData.DbWriter.InsertFuturesOptionTickDataAsync(e.TickData));

        /// <summary>
        /// insert futures eod data
        /// </summary>
        /// <param name="e">FuturesEodDataInsertedEvent</param>
        /// <returns></returns>
        public async Task ExecuteAsync(FuturesEodDataInsertedEvent e) => await DenormalizeAsync(e, Err_MarketDataFeedEventDenormalizer, () => _dbMarketData.DbWriter.InsertFuturesEodDataAsync(e.FuturesEodData));

        /// <summary>
        /// insert vix futures eod data
        /// </summary>
        /// <param name="e">VixFuturesEodDataInsertedEvent</param>
        /// <returns></returns>
        public async Task ExecuteAsync(VixFuturesEodDataInsertedEvent e) => await DenormalizeAsync(e, Err_MarketDataFeedEventDenormalizer, () => _dbMarketData.DbWriter.InsertVixFuturesEodDataAsync(e.VixFuturesTickData));

        /// <summary>
        /// insert trade live feed
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(TradeLiveFeedAddedEvent e) => await DenormalizeAsync(e, Err_MarketDataFeedEventDenormalizer, () => _dbMarketData.DbWriter.InsertTradeLiveFeedAsync(new TradeLiveFeedReadModel(e.OrderId, e.TradeId, false)));

        /// <summary>
        /// delete trade live feed
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(TradeLiveFeedRemovedEvent e) => await DenormalizeAsync(e, Err_MarketDataFeedEventDenormalizer, () => _dbMarketData.DbWriter.InsertTradeLiveFeedAsync(new TradeLiveFeedReadModel(e.OrderId, e.TradeId, false)));


    }
}
