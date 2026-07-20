using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventService;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Log.ServiceApi;
using TomasAI.IFM.Shared.Log;

namespace TomasAI.IFM.Service.MarketDataFeed.EventHandlers;

public class MarketDataFeedEventHandlers(
    IMarketDataApi marketDataApi,
    IMarketDataFeedCommandApi marketDataFeedCommandApi,
    IMarketDataFeedEventProducer eventProducer,
    IStatusConsoleWriter statusConsoleWriter,
    ILogger<MarketDataFeedEventHandlers> logger) 
        : BaseEventServiceHandler(statusConsoleWriter),
        IAsyncEventHandler<MarketDataFeedStartedEvent, MarketDataFeedService>,
        IAsyncEventHandler<MarketDataFeedStartedCompleteEvent, MarketDataFeedService>,
        IAsyncEventHandler<MarketDataFeedStoppedEvent, MarketDataFeedService>,
        IAsyncEventHandler<MarketDataFeedStoppedCompleteEvent, MarketDataFeedService>,
        IAsyncEventHandler<MarketDataFeedResetEvent, MarketDataFeedService>,
        IAsyncEventHandler<MarketDataFeedResetCompleteEvent, MarketDataFeedService>
{
    IMarketDataApi MarketDataApi { get; } = marketDataApi ?? throw new ArgumentNullException(nameof(marketDataApi));
    IMarketDataFeedCommandApi MarketDataFeedCommandApi { get; } = marketDataFeedCommandApi ?? throw new ArgumentNullException(nameof(marketDataFeedCommandApi));
    IMarketDataFeedEventProducer EventProducer { get; } = eventProducer ?? throw new ArgumentNullException(nameof(eventProducer));
    ILogger<MarketDataFeedEventHandlers> Logger { get; } = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task ExecuteAsync(MarketDataFeedStartedEvent e)
    {
        try
        {
            MarketDataApi.Start(async (errorCode, errorMsg) => await WriteConsoleAsync(LogSourceType.MarketDataFeedService, errorCode, errorMsg));
            Logger.LogInformation("{Source}: market data feed started", LogSourceType.MarketDataFeedService);
            await EventProducer.PostEventAsync(e.ToCompletedEvent());
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "{Source}: market data feed start failed", LogSourceType.MarketDataFeedService);
            await EventProducer.PostEventAsync(e.ToFailedEvent(ex));
        }
    }

    public async Task ExecuteAsync(MarketDataFeedStartedCompleteEvent e)
    {
        try
        {
            foreach (var o in e.FuturesContracts!)
            {
                await WriteConsoleAsync(LogSourceType.MarketDataFeedService, $"Starting to stream Futures {o.ContractId}...");
                Logger.LogInformation("{Source}: starting to stream Futures {ContractId}...", LogSourceType.MarketDataFeedService, o.ContractId);
                await Task.Delay(TimeSpan.FromSeconds(2));
                await MarketDataFeedCommandApi.StartFuturesTickDataStreamingAsync(futuresContract: o, valueDate: e.ValueDate, e.ResetStream);
            }
            await MarketDataFeedCommandApi.StartFuturesBarDataStreamingAsync(e.FuturesContracts, e.ValueDate);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "{Source}: data feed start failed", LogSourceType.MarketDataFeedService);
            await WriteConsoleAsync(LogSourceType.MarketDataFeedService, -1, ex.Message);
        }
    }

    public async Task ExecuteAsync(MarketDataFeedStoppedEvent e)
    {
        try
        {
            MarketDataApi.Stop();
            Logger.LogInformation("{Source}: market data feed stopped", LogSourceType.MarketDataFeedService);
            await WriteConsoleAsync(LogSourceType.MarketDataFeedService, "Market data feeds stopped");
            await EventProducer.PostEventAsync(e.ToCompletedEvent());
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.GetErrorMessage(), "{Source}: market data feed stop failed", LogSourceType.MarketDataFeedService);
            await EventProducer.PostEventAsync(e.ToFailedEvent(ex));
        }
    }

    public async Task ExecuteAsync(MarketDataFeedStoppedCompleteEvent e)
    {
        try
        {
            await MarketDataFeedCommandApi.StopFuturesBarDataStreamingAsync(e.ValueDate);
            await WriteConsoleAsync(LogSourceType.MarketDataFeedService, "Futures bar data streaming stopped");
            Logger.LogInformation("{Source}: futures bar data streaming stopped", LogSourceType.MarketDataFeedService);
        }
        catch (Exception ex)
        {
            await WriteConsoleAsync(LogSourceType.MarketDataFeedService, "Stopping futures bar data feed failed");
            Logger.LogError(ex.GetErrorMessage(), "{Source}: stopping futures bar data feed failed", LogSourceType.MarketDataFeedService);
        }
    }

    public async Task ExecuteAsync(MarketDataFeedResetEvent e)
    {
        try
        {
            MarketDataApi.Stop();
            await Task.Delay(TimeSpan.FromSeconds(2));
            MarketDataApi.Start(async (errorCode, errorMsg) => await WriteConsoleAsync(LogSourceType.MarketDataFeedService, errorCode, errorMsg));
            await EventProducer.PostEventAsync(e.ToCompletedEvent());
            await WriteConsoleAsync(LogSourceType.MarketDataFeedService, "Market data feed reset");
            Logger.LogInformation("{Source}: market data feed reset", LogSourceType.MarketDataFeedService);
        }
        catch (Exception ex)
        {
            await WriteConsoleAsync(LogSourceType.MarketDataFeedService, 12003, ex.GetErrorMessage());
            Logger.LogError(ex.GetErrorMessage(), "{Source}: market data feed reset failed", LogSourceType.MarketDataFeedService);
        }
    }

    public async Task ExecuteAsync(MarketDataFeedResetCompleteEvent e)
    {
        try
        {
            foreach (var futuresContract in e.FuturesContracts)
            {
                await WriteConsoleAsync(LogSourceType.MarketDataFeedService, $"Reset streaming of Futures {futuresContract.ContractId}...");
                await Task.Delay(TimeSpan.FromSeconds(2));
                await MarketDataFeedCommandApi.StartFuturesTickDataStreamingAsync(futuresContract: futuresContract, valueDate: e.ValueDate, false);
            }
            await MarketDataFeedCommandApi.StartFuturesBarDataStreamingAsync(e.FuturesContracts, e.ValueDate);
            await Task.Delay(TimeSpan.FromSeconds(1));
            await EventProducer.PostEventAsync(new MarketDataFeedResetStreamingEvent { CommandId = e.CommandId });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "MarketDataFeedResetCompleteEventHandler: data feed reset complete failed");
            await WriteConsoleAsync(LogSourceType.MarketDataFeedService, -1, ex.Message);
        }
    }
    
}
