using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventService;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Log.ServiceApi;
using TomasAI.IFM.Shared.Log;
using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Application.Blackboard;

namespace TomasAI.IFM.Service.MarketDataFeed.EventHandlers;

/// <summary>
/// futures option tick event handlers constructor
/// </summary>
/// <param name="marketDataApi"></param>
/// <param name="eventProducer"></param>
/// <param name="blackboardService"></param>
/// <param name="statusConsoleWriter"></param>
/// <param name="logger"></param>
public class FuturesOptionTickDataEventHandlers(
    IMarketDataApi marketDataApi,
    IMarketDataFeedCommandApi marketDataFeedCommandApi,
    IMarketDataFeedQueryApi marketDataFeedQueryApi,
    IMarketDataFeedEventProducer eventProducer,
    IBlackboardService blackboardService,
    IStatusConsoleWriter statusConsoleWriter,
    ILogger<FuturesOptionTickDataEventHandlers> logger) : BaseEventServiceHandler(statusConsoleWriter),
     IAsyncEventHandler<FuturesOptionTickDataInsertedEvent, MarketDataFeedService>,
     IAsyncEventHandler<FuturesOptionTickDataStreamingStartedEvent, MarketDataFeedService>,
     IAsyncEventHandler<FuturesOptionTickDataStreamingStoppedEvent, MarketDataFeedService>,
     IAsyncEventHandler<FuturesOptionTickPriceDataEvent, MarketDataFeedService>
{
    readonly static SemaphoreSlim _semaphoreSlim = new(1, 1);
    IMarketDataApi MarketDataApi { get; } = IsArgumentNull.Set(marketDataApi);
    IMarketDataFeedCommandApi MarketDataFeedCommandApi = IsArgumentNull.Set(marketDataFeedCommandApi);
    IMarketDataFeedQueryApi MarketDataFeedQueryApi = IsArgumentNull.Set(marketDataFeedQueryApi);
    IMarketDataFeedEventProducer EventProducer { get; } = IsArgumentNull.Set(eventProducer);
    IBlackboardService BlackboardService { get; } = IsArgumentNull.Set(blackboardService);
    ILogger<FuturesOptionTickDataEventHandlers> Logger { get; } = IsArgumentNull.Set(logger);

    /// <summary>
    /// insert futures option tick data
    /// </summary>
    /// <param name="e">futures option tick data inserted event</param>
    /// <returns></returns>
    public async Task ExecuteAsync(FuturesOptionTickDataInsertedEvent e)
    {
        try
        {
            await EventProducer.PostEventAsync(new FuturesOptionTickDataUpdatedEvent { CommandId = e.CommandId, OptionTickData = e.TickData });
        }
        catch (Exception ex)
        {
            await WriteConsoleAsync(LogSourceType.MarketDataFeedService, FuturesOptionTickDataInsertedEvent.ErrorCode, ex.GetErrorMessage());
            Logger.LogError(ex, $"{LogSourceType.MarketDataFeedService}: futures option tick data {e.TickData.ContractId} insert failed");
        }
    }

    /// <summary>
    /// start streaming futures option tick data
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task ExecuteAsync(FuturesOptionTickDataStreamingStartedEvent e)
    {
        try
        {
            var requestId = (await MarketDataFeedQueryApi.GetStreamingRequestIdAsync()).Value.AsInteger;
            // var streamId = MarketDataApi.StreamIds.Add(e.Contract.ContractId);
            if (requestId == -1)
                throw new InvalidOperationException($"{e.GetType().Name}: unable to create stream id from futures option {e.Contract.ContractId}");

            MarketDataApi.StartStreamingFuturesOptionTickData(requestId, e.ValueDate, e.MaturityDate, e.Contract, e.RiskFreeRate);
            BlackboardService.FuturesOptionTickDataStreamingParameter.Set(requestId, new FuturesOptionTickDataStreamingParameter(requestId, e.ValueDate, e.MaturityDate, e.RiskFreeRate, e.BaseContract, e.Contract));
            await WriteConsoleAsync(LogSourceType.MarketDataFeedService, $"futures option {e.Contract.ContractId} streaming started");
            Logger.LogInformation($"{LogSourceType.MarketDataFeedService}: futures option {e.Contract.ContractId} streaming started");
            await EventProducer.PostEventAsync(e.ToCompletedEvent());
        }
        catch (Exception ex)
        {
            await WriteConsoleAsync(LogSourceType.MarketDataFeedService, FuturesOptionTickDataStreamingStartedEvent.ErrorCode, ex.GetErrorMessage());
            Logger.LogError(ex, $"{LogSourceType.MarketDataFeedService}: futures option {e.Contract.ContractId} streaming start failed");
            await EventProducer.PostEventAsync(e.ToFailedEvent(ex));
        }
    }

    /// <summary>
    /// stop streaming futures bar tick data
    /// </summary>
    /// <param name="e">futures bar data streaming stopped event</param>
    /// <returns></returns>
    public async Task ExecuteAsync(FuturesOptionTickDataStreamingStoppedEvent e)
    {
        try
        {
            var requestId = (await MarketDataFeedQueryApi.GetStreamingRequestIdAsync()).Value.AsInteger;
            //var streamId = MarketDataApi.StreamIds[e.ContractId];
            if (requestId != -1)
            {
                MarketDataApi.StopStreamingFuturesOptionTickData(requestId);
                await MarketDataFeedCommandApi.DeleteStreamingRequestIdAsync(e.FeedId);
                //MarketDataApi.StreamIds.Remove(streamId);
                await WriteConsoleAsync(LogSourceType.MarketDataFeedService, $"{e.ContractId} Streaming Stopped");
                Logger.LogInformation($"{LogSourceType.MarketDataFeedService}: futures option {e.ContractId} streaming stopped");
            }
        }
        catch (Exception ex)
        {
            await WriteConsoleAsync(LogSourceType.MarketDataFeedService, 6008, ex.GetErrorMessage());
            Logger.LogError(ex.GetErrorMessage(), $"{LogSourceType.MarketDataFeedService}: futures option {e.ContractId} streaming stop failed");
        }
    }

    /// <summary>
    /// save futures option tick price data if changed since last received futures option tick data
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task ExecuteAsync(FuturesOptionTickPriceDataEvent e)
    {
        var sp = BlackboardService.FuturesOptionTickDataStreamingParameter.Get(e.RequestId);
        if (sp is null)
            return;
        await _semaphoreSlim.WaitAsync();
        try
        {
            var futuresTickData = BlackboardService.FuturesTickData.Get(sp.FuturesContract.ContractId, sp.ValueDate);
            if (futuresTickData is not null && futuresTickData.Price > 0.0)
            {
                var futuresOptionTickData = GetFuturesOptionTickData(e.TickPriceData, futuresTickData.Price);
                if (futuresOptionTickData is not null)
                {
                    var lastFuturesOptionTickData = BlackboardService.FuturesOptionTickData.Get(futuresOptionTickData.ContractId, sp.ValueDate);
                    if (lastFuturesOptionTickData is null
                        || Convert.ToDecimal(lastFuturesOptionTickData.BidPrice) != Convert.ToDecimal(futuresOptionTickData.BidPrice)
                        || Convert.ToDecimal(lastFuturesOptionTickData.AskPrice) != Convert.ToDecimal(futuresOptionTickData.AskPrice))
                    {
                        BlackboardService.FuturesOptionTickData.Set(futuresOptionTickData.ContractId, sp.ValueDate, futuresOptionTickData);
                        await MarketDataFeedCommandApi.InsertFuturesOptionTickDataAsync(sp.FuturesContract, futuresOptionTickData);
                        Logger.LogInformation($"{LogSourceType.MarketDataFeedService}: futures option {sp.FuturesOptionContract.ContractId} price: {futuresOptionTickData.OptionPrice:F2}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            await WriteConsoleAsync(LogSourceType.MarketDataFeedService, 6007, ex.GetErrorMessage());
            Logger.LogError(ex, $"{LogSourceType.MarketDataFeedService}: futures option {sp.FuturesOptionContract.ContractId} streaming failed");
        }
        finally
        {
            _semaphoreSlim.Release();
        }
        return;

        FuturesOptionTickDataV2ReadModel? GetFuturesOptionTickData(FuturesOptionTickPriceDataReadModel o, double futuresPrice)
        {
            var daysToExpiry = sp.MaturityDate.DayNumber - sp.ValueDate.DayNumber;
            var optionCalculator = new OptionCalculator(sp.ValueDate, sp.MaturityDate);
            var optionValue = (o.BidPrice + o.AskPrice) / 2;
            var optionGreeks = optionCalculator.GetOptionGreeks(sp.FuturesOptionContract.OptionType, futuresPrice, sp.FuturesOptionContract.StrikePrice, optionValue, sp.RiskFreeRate);
            if (!optionGreeks.Success) 
                return default;
            var timeValue = daysToExpiry / 365.0;
            return new FuturesOptionTickDataV2ReadModel(
                sp.FuturesOptionContract.ContractId,
                sp.ValueDate,
                o.TickTime,
                TimeOnly.FromDateTime(o.TickDate),
                optionValue,
                o.BidPrice,
                o.AskPrice,
                o.BidSize,
                o.AskSize,
                optionGreeks.ImpliedVolatility,
                futuresPrice,
                optionGreeks.Delta,
                optionGreeks.Gamma,
                optionGreeks.Vega,
                optionGreeks.Theta,
                optionGreeks.Rho);
        }

    }

}
