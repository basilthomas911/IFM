using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Shared.MarketData.Events;
using TomasAI.IFM.Shared.MarketData.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.Event.Denormalizer
{
    public class MarketDataEventDenormalizer : BaseEventDenormalizer,
        IAsyncEventHandler<FuturesContractAddedEvent>,
        IAsyncEventHandler<FuturesContractRemovedEvent>,
        IAsyncEventHandler<FuturesContractChangedEvent>,
        IAsyncEventHandler<FuturesOptionContractAddedEvent>,
        IAsyncEventHandler<FuturesOptionContractRemovedEvent>,
        IAsyncEventHandler<FuturesOptionContractChangedEvent>,
        IAsyncEventHandler<YieldCurveRateAddedEvent>,
        IAsyncEventHandler<YieldCurveRateChangedEvent>,
        IAsyncEventHandler<YieldCurveRateRemovedEvent>,
        IAsyncEventHandler<YieldCurveRatesImportedEvent>
    {
        private const int Err_MarketDataEventDenormalizer = 5002;
        private readonly IMarketDataDbContext _dbMarketData;
        private readonly IMarketDataEventProducer _eventProducer;

        public MarketDataEventDenormalizer(IDbContextFactory dbFactory, IMarketDataEventProducer eventProducer, ILogger<MarketDataEventDenormalizer> logger):base(logger)
        {
            _dbMarketData = dbFactory.MarketDataDb as IMarketDataDbContext;
            _eventProducer = eventProducer;
            SetEventProducer(e => _eventProducer.PostEventAsync(e));
        }

        /// <summary>
        /// insert futures contract into query store
        /// </summary>
        /// <param name="e">FuturesContractAddedEvent</param>
        /// <returns></returns>
        public async Task ExecuteAsync(FuturesContractAddedEvent e)
            => await DenormalizeAsync(e, Err_MarketDataEventDenormalizer, () => _dbMarketData.DbWriter.InsertFuturesContractAsync(e.Contract));

        /// <summary>
        /// update futures contratc in query store
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(FuturesContractChangedEvent e)
            => await DenormalizeAsync(e, Err_MarketDataEventDenormalizer, () => _dbMarketData.DbWriter.UpdateFuturesContractAsync(e.OriginalContractId, e.Contract));


        /// <summary>
        /// delete futures contract from query store
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(FuturesContractRemovedEvent e)
            => await DenormalizeAsync(e, Err_MarketDataEventDenormalizer, () => _dbMarketData.DbWriter.DeleteFuturesContractAsync(e.ContractId));


        /// <summary>
        /// insert futures option contract into query store
        /// </summary>
        /// <param name="e">FuturesOptionContractAddedEvent</param>
        /// <returns></returns>
        public async Task ExecuteAsync(FuturesOptionContractAddedEvent e)
            => await DenormalizeAsync(e, Err_MarketDataEventDenormalizer, () => _dbMarketData.DbWriter.InsertFuturesOptionContractAsync(e.Contract));

        /// <summary>
        /// delete futures option contract from query store
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(FuturesOptionContractRemovedEvent e)
            => await DenormalizeAsync(e, Err_MarketDataEventDenormalizer, () => _dbMarketData.DbWriter.DeleteFuturesOptionContractAsync(e.ContractId));

        /// <summary>
        /// update futures option contract in query store
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(FuturesOptionContractChangedEvent e)
            => await DenormalizeAsync(e, Err_MarketDataEventDenormalizer, () => _dbMarketData.DbWriter.UpdateFuturesOptionContractAsync(e.OriginalContractId, e.Contract));

        /// <summary>
        /// insert yield curve rate into query store
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(YieldCurveRateAddedEvent e)
             => await DenormalizeAsync(e, Err_MarketDataEventDenormalizer, () => _dbMarketData.DbWriter.InsertYieldCurveRateAsync(e.YieldCurveRate));
        
        /// <summary>
        /// update yield curve rate in query store
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(YieldCurveRateChangedEvent e)
            => await DenormalizeAsync(e, Err_MarketDataEventDenormalizer, () => _dbMarketData.DbWriter.UpdateYieldCurveRateAsync(e.YieldCurveRate));

        /// <summary>
        /// delete yeild curve rate from query store
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(YieldCurveRateRemovedEvent e)
            => await DenormalizeAsync(e, Err_MarketDataEventDenormalizer, () => _dbMarketData.DbWriter.DeleteYieldCurveRateAsync(e.ValueDate));

        /// <summary>
        /// import yield curve rates into query store
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(YieldCurveRatesImportedEvent e)
            => await DenormalizeAsync(e, Err_MarketDataEventDenormalizer, () => _dbMarketData.DbWriter.InsertYieldCurveRatesAsync(e.YieldCurveRates.ToList()));

    }
}
