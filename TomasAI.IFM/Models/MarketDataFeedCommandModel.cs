using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.MarketData.ServiceApi;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.EventConsumer;
using TomasAI.IFM.Shared.MarketDataFeed;
using QLNet;

namespace TomasAI.IFM.Models
{
    public class MarketDataFeedCommandModel : BaseModel<MarketDataFeedCommandModel>
    {
        readonly IMarketDataFeedCommandApi _marketDataFeedCommandApi;
        readonly IMarketDataQueryApi _marketDataQueryApi;
        readonly IMarketDataFeedQueryApi _marketDataFeedQueryApi;
        readonly IFuturesEodDataUIEventConsumer _futuresEodDataEventConsumer;
        readonly IFuturesTradeSignalUIEventConsumer _futuresTradeSignalEventConsumer;
        readonly IFuturesOptionTickDataUIEventConsumer _futuresOptionTickDataEventConsumer;
        readonly IMarketDataFeedResetUIEventConsumer _marketDataFeedResetEventConsumer;
        readonly IFuturesBarDataUIEventConsumer _futuresBarDataEventConsumer;

        public MarketDataFeedCommandModel(
            IMarketDataFeedCommandApi marketDataFeedCommandApi,
            IMarketDataQueryApi marketDataQueryApi,
            IMarketDataFeedQueryApi marketDataFeedQueryApi,
            IFuturesEodDataUIEventConsumer futuresEodDataEventConsumer,
            IFuturesTradeSignalUIEventConsumer futuresTradeSignalEventConsumer,
            IFuturesOptionTickDataUIEventConsumer futuresOptionTickDataEventConsumer,
            IMarketDataFeedResetUIEventConsumer marketDataFeedResetEventConsumer,
            IFuturesBarDataUIEventConsumer futuresBarDataEventConsumer)
        {
            _marketDataFeedCommandApi = marketDataFeedCommandApi;
            _marketDataQueryApi = marketDataQueryApi;
            _marketDataFeedQueryApi = marketDataFeedQueryApi;
            _futuresEodDataEventConsumer = futuresEodDataEventConsumer;
            _futuresTradeSignalEventConsumer = futuresTradeSignalEventConsumer;
            _futuresOptionTickDataEventConsumer = futuresOptionTickDataEventConsumer;
            _marketDataFeedResetEventConsumer = marketDataFeedResetEventConsumer;
            _futuresBarDataEventConsumer = futuresBarDataEventConsumer;
        }

        /// <summary>
        /// add trade live feed
        /// </summary>
        /// <param name="orderId"></param>
        /// <param name="tradeId"></param>
        public async Task AddTradeLiveFeedAsync(int orderId, int tradeId) 
            => await ExecuteCommandAsync(() =>  _marketDataFeedCommandApi.AddTradeLiveFeedAsync(orderId, tradeId));

        /// <summary>
        /// remove trade live feed
        /// </summary>
        /// <param name="orderId"></param>
        /// <param name="tradeId"></param>
        public async Task RemoveTradeLiveFeedAsync(int orderId, int tradeId) 
            => await ExecuteCommandAsync( ()=> _marketDataFeedCommandApi.RemoveTradeLiveFeedAsync(orderId, tradeId));

        /// <summary>
        /// remove all trade live feeds for order
        /// </summary>
        /// <param name="orderId"></param>
        public async Task RemoveTradeLiveFeedsAsync(int orderId) 
            => await ExecuteCommandAsync( () => _marketDataFeedCommandApi.RemoveTradeLiveFeedsAsync(orderId));

        /// <summary>
        /// start market data feed streaming
        /// </summary>
        /// <param name="futuresContracts"></param>
        /// <param name="valueDate"></param>
        public async Task StartDataFeedAsync(ICollection<FuturesContractViewModel> futuresContracts, DateTime valueDate) 
            => await ExecuteCommandAsync( () => _marketDataFeedCommandApi.StartMarketDataFeedAsync(futuresContracts, valueDate));

        /// <summary>
        /// stop market data feed streaming
        /// </summary>
        /// <param name="stopStreamingAction"></param>
        public async Task StopDataFeedAsync(Action stopStreamingAction)
        {
            stopStreamingAction();
            await Task.Delay(TimeSpan.FromSeconds(2));
            await ExecuteCommandAsync( _marketDataFeedCommandApi.StopMarketDataFeedAsync );
        }

        /// <summary>
        /// reset market data feed streaming
        /// </summary>
        /// <param name="futuresContracts"></param>
        /// <param name="valueDate"></param>
        public async Task ResetDataFeedAsync(ICollection<FuturesContractViewModel> futuresContracts, DateTime valueDate)
            => await ExecuteCommandAsync( () => _marketDataFeedCommandApi.ResetMarketDataFeedAsync(futuresContracts, valueDate));

        /// <summary>
        /// stop streaming futures tick data
        /// </summary>
        /// <param name="contractId"></param>
        public async Task StopStreamingFuturesTickDataAsync(string contractId)
            => await ExecuteCommandAsync( () => _marketDataFeedCommandApi.StopFuturesTickDataStreamingAsync(contractId));

        /// <summary>
        /// start streaming futures options tick data
        /// </summary>
        /// <param name="contractIds"></param>
        /// <param name="valueDate"></param>
        /// <param name="maturityDate"></param>
        /// <param name="riskFreeRate"></param>
        /// <returns></returns>
        public async Task StartStreamingFuturesOptionTickDataAsync(Dictionary<StreamId, string> streamIds, FuturesContractViewModel baseContract, DateTime valueDate, DateTime maturityDate, double riskFreeRate, Action onCompleted)
            => await ExecuteAsync(async () => {
                foreach (var e in streamIds)
                {
                    var streamId = e.Key;
                    var contractId = e.Value;
                    var qfContractResult = await _marketDataQueryApi.GetFuturesOptionContractAsync(e.Value);
                    if (qfContractResult is not null && qfContractResult.Success && qfContractResult.Value is not null)
                    {
                        var qfContract = qfContractResult.Value;
                        var contractResult = await _marketDataFeedQueryApi.GetFuturesOptionContractAsync(contractId, qfContract);
                        if (contractResult is not null && contractResult.Success && contractResult.Value is not null)
                        {
                            var contract = contractResult.Value;
                            await _marketDataFeedCommandApi.StartFuturesOptionTickDataStreamingAsync(streamId, contract, baseContract, valueDate, maturityDate, riskFreeRate);
                            await Task.Delay(TimeSpan.FromSeconds(2));
                            continue;
                        }
                        RaiseError(contractResult?.ErrorCode ?? 9999, $"Futures option contract definition: {contractId} not found");
                        return;
                    }
                    RaiseError(qfContractResult?.ErrorCode ?? 9998, $"Futures option contract: {contractId} not found");
                    return;
                }
                onCompleted?.Invoke();
            });

        /// <summary>
        /// stop streaming futures tick data
        /// </summary>
        /// <param name="streamId"></param>
        public async Task StopStreamingFuturesOptionTickDataAsync(StreamId streamId, string contractId) 
            => await ExecuteCommandAsync( () => _marketDataFeedCommandApi.StopFuturesOptionTickDataStreamingAsync(streamId, contractId) );

        /// <summary>
        /// delete futures bar data less than value date
        /// </summary>
        /// <param name="valueDate"></param>
        public async Task DeleteFuturesBarDataAsync (DateTime valueDate)
            => await ExecuteCommandAsync( () => _marketDataFeedCommandApi.DeleteFuturesBarDataAsync(valueDate) );

        /// <summary>
        /// start listening for futures eod data updates
        /// </summary>
        /// <param name="siteId"></param>
        /// <param name="listenerAction"></param>
        public async Task StartFuturesEodDataEventConsumerAsync(Guid siteId, Action<FuturesEodDataInsertedCompleteEvent> listenerAction)
            => await ExecuteAsync( () => _futuresEodDataEventConsumer.StartAsync(siteId, listenerAction) );

        /// <summary>
        /// stop listening for futures eod data updates
        /// </summary>
        /// <param name="siteId"></param>
        public async Task StopFuturesEodDataEventConsumerAsync(Guid siteId)
            => await ExecuteAsync( () => _futuresEodDataEventConsumer.StopAsync(siteId) );

        /// <summary>
        /// start listening for futures trade signal updates
        /// </summary>
        /// <param name="siteId"></param>
        /// <param name="listenerAction"></param>
        public async Task StartFuturesTradeSignalEventConsumerAsync(Guid siteId, Action<FuturesTradeSignalUpdatedCompleteEvent> listenerAction)
            => await ExecuteAsync( () => _futuresTradeSignalEventConsumer.StartAsync(siteId, listenerAction) );

        /// <summary>
        /// stop listening for futures trade signal updates
        /// </summary>
        /// <param name="siteId"></param>
        public async Task StopFuturesTradeSignalEventConsumerAsync(Guid siteId)
            => await ExecuteAsync( () => _futuresTradeSignalEventConsumer.StopAsync(siteId) );

        /// <summary>
        /// start listening for futures bar data inserted complete
        /// </summary>
        /// <param name="siteId"></param>
        /// <param name="listenerAction"></param>
        public async Task StartFuturesBarDataEventConsumerAsync(Guid siteId, Action<FuturesBarDataInsertedCompleteEvent> listenerAction)
            => await ExecuteAsync( () => _futuresBarDataEventConsumer.StartAsync(siteId, listenerAction) );

        /// <summary>
        /// stop listening for futures bar data inserted complete
        /// </summary>
        /// <param name="siteId"></param>
        public async Task StopFuturesBarDataEventConsumerAsync(Guid siteId)
            => await ExecuteAsync( () => _futuresBarDataEventConsumer.StopAsync(siteId) );

        /// <summary>
        /// start listening for market data feed reset event
        /// </summary>
        /// <param name="listenerAction"></param>
        public async Task StartMarketDataFeedResetListenerAsync(Action<MarketDataFeedResetStreamingEvent> listenerAction) 
            => await ExecuteAsync( () => _marketDataFeedResetEventConsumer.StartAsync(listenerAction) );

        /// <summary>
        /// stop listening for market data feed reset event
        /// </summary>
        public async Task StopMarketDataFeedResetListenerAsync() 
            => await ExecuteAsync( _marketDataFeedResetEventConsumer.StopAsync );

        /// <summary>
        /// start listening for futures option tick data updates
        /// </summary>
        /// <param name="listenerAction"></param>
        public async Task StartFuturesOptionTickDataListenerAsync(Action<FuturesOptionTickDataUpdatedEvent> listenerAction) 
            => await ExecuteAsync( () => _futuresOptionTickDataEventConsumer.StartAsync(listenerAction) );

        /// <summary>
        /// stop listening for futures option tick data updates
        /// </summary>
        public async Task StopFuturesOptionTickDataListenerAsync() 
            => await ExecuteAsync( _futuresOptionTickDataEventConsumer.StopAsync );

        /// <summary>
        /// start streaming futures options quote data
        /// </summary>
        /// <param name="quoteId"></param>
        /// <param name="futuresOptionQuotes"></param>
        /// <param name="futuresOptionContracts"></param>
        /// <returns></returns>
        public async Task StartStreamingFuturesOptionQuoteDataAsync(Guid quoteId, FuturesOptionQuoteReadModel[] futuresOptionQuotes, FuturesOptionContractReadModel[] futuresOptionContracts)
           => await ExecuteCommandAsync(() => _marketDataFeedCommandApi.StartFuturesOptionQuoteDataStreamingAsync(quoteId, futuresOptionQuotes, futuresOptionContracts));   

        /// <summary>
        /// stop streaming futures quote data
        /// </summary>
        /// <param name="quoteId"></param>
        public async Task StopStreamingFuturesOptionQuoteDataAsync(Guid quoteId, Action onCompleted)
            => await ExecuteCommandAsync(() => _marketDataFeedCommandApi.StopFuturesOptionQuoteDataStreamingAsync(quoteId), onCompleted);


    }
}
