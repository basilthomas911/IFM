using TomasAI.IFM.Shared.MarketData.ServiceApi;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.UI.EventConsumer;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.Trade.Events;

namespace TomasAI.IFM.UI.Net.Models;

public class MarketDataFeedCommandModel(
    IMarketDataFeedCommandApi marketDataFeedCommandApi,
    IMarketDataQueryApi marketDataQueryApi,
    IMarketDataFeedQueryApi marketDataFeedQueryApi,
    IFuturesEodDataUIEventConsumer futuresEodDataEventConsumer,
    IFuturesTradeSignalUIEventConsumer futuresTradeSignalEventConsumer,
    IFuturesOptionTickDataUIEventConsumer futuresOptionTickDataEventConsumer,
    IMarketDataFeedResetUIEventConsumer marketDataFeedResetEventConsumer,
    IFuturesBarDataUIEventConsumer futuresBarDataEventConsumer) : BaseModel<MarketDataFeedCommandModel>
{
    readonly IMarketDataFeedCommandApi _marketDataFeedCommandApi = marketDataFeedCommandApi;
    readonly IMarketDataQueryApi _marketDataQueryApi = marketDataQueryApi;
    readonly IMarketDataFeedQueryApi _marketDataFeedQueryApi = marketDataFeedQueryApi;
    readonly IFuturesEodDataUIEventConsumer _futuresEodDataEventConsumer = futuresEodDataEventConsumer;
    readonly IFuturesTradeSignalUIEventConsumer _futuresTradeSignalEventConsumer = futuresTradeSignalEventConsumer;
    readonly IFuturesOptionTickDataUIEventConsumer _futuresOptionTickDataEventConsumer = futuresOptionTickDataEventConsumer;
    readonly IMarketDataFeedResetUIEventConsumer _marketDataFeedResetEventConsumer = marketDataFeedResetEventConsumer;
    readonly IFuturesBarDataUIEventConsumer _futuresBarDataEventConsumer = futuresBarDataEventConsumer;

    /// <summary>
    /// add trade live feed
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    public async Task AddTradeLiveFeedAsync(int orderId, int tradeId, DateOnly valueDate) 
        => await ExecuteCommandAsync(() =>  _marketDataFeedCommandApi.AddTradeLiveFeedAsync(orderId, tradeId, valueDate));

    /// <summary>
    /// remove trade live feed
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    public async Task RemoveTradeLiveFeedAsync(int orderId, int tradeId, DateOnly valueDate)
        => await ExecuteCommandAsync(() => _marketDataFeedCommandApi.RemoveTradeLiveFeedAsync(orderId, tradeId, valueDate));
    public async Task RemoveTradeLiveFeedsAsync(int orderId)
        => await ExecuteCommandAsync(() => _marketDataFeedCommandApi.RemoveTradeLiveFeedsAsync(orderId));

    /// <summary>
    /// start market data feed streaming
    /// </summary>
    /// <param name="futuresContracts"></param>
    /// <param name="valueDate"></param>
    public async Task StartDataFeedAsync(ICollection<FuturesContractV2ReadModel> futuresContracts, DateOnly valueDate) 
        => await ExecuteCommandAsync( () => _marketDataFeedCommandApi.StartMarketDataFeedAsync(futuresContracts, valueDate));

    /// <summary>
    /// stop market data feed streaming
    /// </summary>
    /// <param name="valueDate"></param>
    /// <param name="stopStreamingAction"></param>
    public async Task StopDataFeedAsync(DateOnly valueDate, Action stopStreamingAction)
    {
        stopStreamingAction();
        await Task.Delay(TimeSpan.FromSeconds(2));
        await ExecuteCommandAsync( () => _marketDataFeedCommandApi.StopMarketDataFeedAsync(valueDate) );
    }

    /// <summary>
    /// reset market data feed streaming
    /// </summary>
    /// <param name="futuresContracts"></param>
    /// <param name="valueDate"></param>
    public async Task ResetDataFeedAsync(ICollection<FuturesContractV2ReadModel> futuresContracts, DateOnly valueDate)
        => await ExecuteCommandAsync( () => _marketDataFeedCommandApi.ResetMarketDataFeedAsync(futuresContracts, valueDate));

    /// <summary>
    /// stop streaming futures tick data
    /// </summary>
    /// <param name="contractId"></param>
    public async Task StopStreamingFuturesTickDataAsync(string contractId, DateOnly valueDate)
        => await ExecuteCommandAsync( () => _marketDataFeedCommandApi.StopFuturesTickDataStreamingAsync(contractId, valueDate));

    /// <summary>
    /// Starts streaming tick data for futures options contracts asynchronously.
    /// </summary>
    /// <remarks>This method retrieves futures options contract definitions and starts streaming tick data for
    /// each contract specified in the <paramref name="feedIds"/> dictionary. If a contract definition cannot be found
    /// or an error occurs during the process, the method raises an error and terminates further processing.</remarks>
    /// <param name="feedIds">A dictionary where the key represents the feed identifier and the value represents the contract identifier.</param>
    /// <param name="baseContract">The base futures contract associated with the options contracts.</param>
    /// <param name="valueDate">The value date for the options contracts.</param>
    /// <param name="maturityDate">The maturity date for the options contracts.</param>
    /// <param name="riskFreeRate">The risk-free interest rate used in calculations.</param>
    /// <param name="onCompleted">An action to be invoked upon successful completion of the streaming process.</param>
    /// <returns></returns>
    public async Task StartStreamingFuturesOptionTickDataAsync(Dictionary<FuturesOptionTickEntityId, string> feedIds, FuturesContractV2ReadModel baseContract, DateOnly valueDate, DateOnly maturityDate, double riskFreeRate, Action onCompleted)
        => await ExecuteAsync(async () => {
            foreach (var e in feedIds)
            {
                var entityId = e.Key;
                var contractId = e.Value;
                var qfContractResult = await _marketDataQueryApi.GetFuturesOptionContractAsync(e.Value);
                if (qfContractResult is not null && qfContractResult.Success && qfContractResult.Value is not null)
                {
                    var qfContract = qfContractResult.Value;
                    var contractResult = await _marketDataFeedQueryApi.GetFuturesOptionContractAsync(contractId, qfContract);
                    if (contractResult is not null && contractResult.Success && contractResult.Value is not null)
                    {
                        var contract = contractResult.Value;
                        await _marketDataFeedCommandApi.StartFuturesOptionTickDataStreamingAsync(entityId, contract, baseContract, valueDate, maturityDate, riskFreeRate);
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
    /// <param name="feedId"></param>
    public async Task StopStreamingFuturesOptionTickDataAsync(FuturesOptionTickEntityId entityId, string contractId) 
        => await ExecuteCommandAsync( () => _marketDataFeedCommandApi.StopFuturesOptionTickDataStreamingAsync(entityId, contractId) );

    /// <summary>
    /// delete futures bar data less than value date
    /// </summary>
    /// <param name="valueDate"></param>
    public async Task DeleteFuturesBarDataAsync (FuturesBarDataId id)
        => await ExecuteCommandAsync( () => _marketDataFeedCommandApi.DeleteFuturesBarDataAsync(id) );

    /// <summary>
    /// start listening for futures eod data updates
    /// </summary>
    /// <param name="siteId"></param>
    /// <param name="listenerAction"></param>
    public async Task StartFuturesEodDataEventConsumerAsync(Guid siteId, Action<FuturesEodDataInsertedCompleteEvent> listenerAction)
        => await ExecuteValueTaskAsync( () => _futuresEodDataEventConsumer.StartAsync(listenerAction) );

    /// <summary>
    /// stop listening for futures eod data updates
    /// </summary>
    /// <param name="siteId"></param>
    public async Task StopFuturesEodDataEventConsumerAsync(Guid siteId)
        => await ExecuteValueTaskAsync( () => _futuresEodDataEventConsumer.StopAsync() );

    /// <summary>
    /// start listening for futures trade signal updates
    /// </summary>
    /// <param name="siteId"></param>
    /// <param name="listenerAction"></param>
    public async Task StartFuturesTradeSignalEventConsumerAsync(Guid siteId, Action<FuturesTradeSignalUpdatedCompleteEvent> listenerAction)
        => await ExecuteValueTaskAsync( () => _futuresTradeSignalEventConsumer.StartAsync( listenerAction) );

    /// <summary>
    /// stop listening for futures trade signal updates
    /// </summary>
    /// <param name="siteId"></param>
    public async Task StopFuturesTradeSignalEventConsumerAsync(Guid siteId)
        => await ExecuteValueTaskAsync( () => _futuresTradeSignalEventConsumer.StopAsync() );

    /// <summary>
    /// start listening for futures bar data inserted complete
    /// </summary>
    /// <param name="siteId"></param>
    /// <param name="listenerAction"></param>
    public async Task StartFuturesBarDataEventConsumerAsync(Guid siteId, Action<FuturesBarDataInsertedCompleteEvent> listenerAction)
        => await ExecuteValueTaskAsync( () => _futuresBarDataEventConsumer.StartAsync( listenerAction) );

    /// <summary>
    /// stop listening for futures bar data inserted complete
    /// </summary>
    /// <param name="siteId"></param>
    public async Task StopFuturesBarDataEventConsumerAsync(Guid siteId)
        => await ExecuteValueTaskAsync( () => _futuresBarDataEventConsumer.StopAsync() );

    /// <summary>
    /// start listening for market data feed reset event
    /// </summary>
    /// <param name="listenerAction"></param>
    public async Task StartMarketDataFeedResetListenerAsync(Action<MarketDataFeedResetStreamingEvent> listenerAction) 
        => await ExecuteValueTaskAsync( () => _marketDataFeedResetEventConsumer.StartAsync(listenerAction) );

    /// <summary>
    /// stop listening for market data feed reset event
    /// </summary>
    public async Task StopMarketDataFeedResetListenerAsync() 
        => await ExecuteValueTaskAsync( _marketDataFeedResetEventConsumer.StopAsync );

    /// <summary>
    /// start listening for futures option tick data updates
    /// </summary>
    /// <param name="listenerAction"></param>
    public async Task StartFuturesOptionTickDataListenerAsync(Action<OptionTradeTickPriceDataUpdatedEvent> listenerAction) 
        => await ExecuteValueTaskAsync( () => _futuresOptionTickDataEventConsumer.StartAsync(listenerAction) );

    /// <summary>
    /// stop listening for futures option tick data updates
    /// </summary>
    public async Task StopFuturesOptionTickDataListenerAsync() 
        => await ExecuteValueTaskAsync( _futuresOptionTickDataEventConsumer.StopAsync );

    /// <summary>
    /// start streaming futures options quote data
    /// </summary>
    /// <param name="quoteId"></param>
    /// <param name="futuresOptionQuotes"></param>
    /// <param name="futuresOptionContracts"></param>
    /// <returns></returns>
    public async Task StartStreamingFuturesOptionQuoteDataAsync(int quoteId, FuturesOptionQuoteReadModel[] futuresOptionQuotes, FuturesOptionContractReadModel[] futuresOptionContracts)
       => await ExecuteCommandAsync(() => _marketDataFeedCommandApi.StartFuturesOptionQuoteDataStreamingAsync(quoteId, futuresOptionQuotes, futuresOptionContracts));   

    /// <summary>
    /// stop streaming futures quote data
    /// </summary>
    /// <param name="quoteId"></param>
    public async Task StopStreamingFuturesOptionQuoteDataAsync(int quoteId, Action onCompleted)
        => await ExecuteCommandAsync(() => _marketDataFeedCommandApi.StopFuturesOptionQuoteDataStreamingAsync(quoteId), onCompleted);


}
