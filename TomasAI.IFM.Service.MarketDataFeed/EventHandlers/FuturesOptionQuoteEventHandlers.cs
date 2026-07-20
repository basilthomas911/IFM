using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventService;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Log.ServiceApi;
using TomasAI.IFM.Shared.Log;
using TomasAI.IFM.Application.Blackboard;

namespace TomasAI.IFM.Service.MarketDataFeed.EventHandlers;

public class FuturesOptionQuoteEventHandlers(
    IMarketDataSnapshotApi marketDataSnapshotApi,
    IMarketDataFeedCommandApi marketDataFeedCommandApi,
    IMarketDataFeedEventProducer eventProducer,
    IBlackboardService blackboardService,
    IStatusConsoleWriter statusConsoleWriter,
    ILogger<FuturesOptionQuoteEventHandlers> logger) 
    : BaseEventServiceHandler(statusConsoleWriter),
         IAsyncEventHandler<FuturesOptionQuoteDataInsertedCompleteEvent, MarketDataFeedService>,
         IAsyncEventHandler<FuturesOptionQuoteDataStreamingStartedCompleteEvent, MarketDataFeedService>,
         IAsyncEventHandler<FuturesOptionQuoteDataStreamingStoppedCompleteEvent, MarketDataFeedService>,
         IAsyncEventHandler<FuturesOptionQuoteStreamingDataEvent, MarketDataFeedService>
{
    IMarketDataSnapshotApi _marketDataSnapshotApi = IsArgumentNull.Set(marketDataSnapshotApi);
    IMarketDataFeedCommandApi _marketDataFeedCommandApi = IsArgumentNull.Set(marketDataFeedCommandApi);
    IMarketDataFeedEventProducer _eventProducer { get; } = IsArgumentNull.Set(eventProducer);
    IBlackboardService _blackboardService { get; } = IsArgumentNull.Set(blackboardService);
    ILogger<FuturesOptionQuoteEventHandlers> _logger { get; } = IsArgumentNull.Set(logger);

    /// <summary>
    /// insert futures option quote data
    /// </summary>
    /// <param name="e">futures option quote data inserted event</param>
    /// <returns></returns>
    public async Task ExecuteAsync(FuturesOptionQuoteDataInsertedCompleteEvent e)
    {
        try
        {
            await _eventProducer.PostEventAsync(new FuturesOptionQuoteDataUpdatedEvent { CommandId = e.CommandId, OptionQuoteData = e.OptionQuoteData });
        }
        catch (Exception ex)
        {
            await WriteConsoleAsync(LogSourceType.MarketDataFeedService, e.ErrorCode, ex.GetErrorMessage());
            _logger.LogError(ex, $"{LogSourceType.MarketDataFeedService}: {e.GetType().Name} {e.OptionQuoteData.ContractId}  failed");
        }
    }

    /// <summary>
    /// start streaming futures option quote data
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task ExecuteAsync(FuturesOptionQuoteDataStreamingStartedCompleteEvent e)
    {
        try
        {
            _marketDataSnapshotApi.Start();
            foreach (var o in e.FuturesOptionQuotes) 
            {
                var optionContract = e.FuturesOptionContracts.Where(a => a.ContractId == o.ContractId).FirstOrDefault();
                _marketDataSnapshotApi.StartStreamingFuturesOptionQuoteData(o.RequestId, optionContract, o);
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
            _blackboardService.FuturesOptionQuote.Set(e.QuoteId, e.FuturesOptionQuotes);
            await WriteConsoleAsync(LogSourceType.MarketDataFeedService, $"{e.GetType().Name}: {e.QuoteId}");
            _logger.LogInformation($"{LogSourceType.MarketDataFeedService}: {e.GetType().Name}: {e.QuoteId}");
        }
        catch (Exception ex)
        {
            await WriteConsoleAsync(LogSourceType.MarketDataFeedService, e.ErrorCode, ex.Message);
            _logger.LogError(ex, $"{LogSourceType.MarketDataFeedService}: {e.GetType().Name}: {e.QuoteId} failed");
        }
    }

    /// <summary>
    /// stop futures option quote data streaming
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task ExecuteAsync(FuturesOptionQuoteDataStreamingStoppedCompleteEvent e)
    {
        try
        {
            foreach (var o in e.FuturesOptionQuotes)
            {
                _marketDataSnapshotApi.StopStreamingFuturesOptionQuoteData(o.RequestId);
                _blackboardService.FuturesOptionQuoteData.Clear(o.Id);
                await _marketDataFeedCommandApi.DeleteStreamingRequestIdAsync(new FeedId(o.RequestId));
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
            _marketDataSnapshotApi.Stop();
            _blackboardService.FuturesOptionQuote.Clear(e.QuoteId);
            await WriteConsoleAsync(LogSourceType.MarketDataFeedService, $"{e.GetType().Name}: {e.QuoteId}");
            _logger.LogInformation($"{LogSourceType.MarketDataFeedService}: {e.GetType().Name}: {e.QuoteId}");
        }
        catch (Exception ex)
        {
            await WriteConsoleAsync(LogSourceType.MarketDataFeedService, e.ErrorCode,ex.Message);
            _logger.LogError(ex, $"{LogSourceType.MarketDataFeedService}: {e.GetType().Name}: {e.QuoteId} failed");
        }
    }

    /// <summary>
    /// post updated futures option streaming quote data
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task ExecuteAsync(FuturesOptionQuoteStreamingDataEvent e)
    {
        try
        {
            var futuresOptionQuoteMap  = _blackboardService.FuturesOptionQuote.Get(e.QuoteId);
            var optionContractId = futuresOptionQuoteMap[e.RequestId].ContractId;
            await  _marketDataFeedCommandApi.InsertFuturesOptionQuoteDataAsync(e.QuoteId, optionContractId, e.QuoteData);
            await WriteConsoleAsync(LogSourceType.MarketDataFeedService, $"Quote Data: {optionContractId}");
            _logger.LogInformation($"{LogSourceType.MarketDataFeedService}: {e.GetType().Name}: {optionContractId}");
        }
        catch (Exception ex)
        {
            await WriteConsoleAsync(LogSourceType.MarketDataFeedService, e.ErrorCode, ex.GetErrorMessage());
            _logger.LogError(ex, $"{LogSourceType.MarketDataFeedService}: {e.GetType().Name}: {e.QuoteId} failed");
        }
    }

}
