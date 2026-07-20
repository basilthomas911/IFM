using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventService;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.MarketDataAnalytics.ServiceApi;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Log.ServiceApi;
using TomasAI.IFM.Shared.Log;

namespace TomasAI.IFM.Service.MarketDataFeed.EventHandlers;

public class FuturesBarDataEventHandlers(
    IMarketDataFeedCommandApi marketDataFeedCommandApi,
    IMarketDataFeedQueryApi marketDataFeedQueryApi,
    IMarketDataAnalyticsQueryApi marketDataAnalyticsQueryApi,
    IFuturesBarDataTimer futuresBarDataTimer,
    IStatusConsoleWriter statusConsoleWriter,
    ILogger<FuturesBarDataEventHandlers> logger) 
    : BaseEventServiceHandler(statusConsoleWriter),
        IAsyncEventHandler<FuturesBarDataStreamingStartedEvent, MarketDataFeedService>,
        IAsyncEventHandler<FuturesBarDataStreamingStoppedEvent, MarketDataFeedService>
{
    readonly IMarketDataFeedCommandApi _marketDataFeedCommandApi = IsArgumentNull.Set(marketDataFeedCommandApi);
    readonly IMarketDataFeedQueryApi _marketDataFeedQueryApi = IsArgumentNull.Set(marketDataFeedQueryApi);
    readonly IMarketDataAnalyticsQueryApi _marketDataAnalyticsQueryApi = IsArgumentNull.Set(marketDataAnalyticsQueryApi);
    readonly ILogger<FuturesBarDataEventHandlers> _logger = IsArgumentNull.Set(logger);
    readonly IFuturesBarDataTimer _futuresBarDataTimer = IsArgumentNull.Set(futuresBarDataTimer);

    /// <summary>
    /// start streaming futures bar data
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task ExecuteAsync(FuturesBarDataStreamingStartedEvent e)
    {
        try
        {
            await InsertFuturesBarDataAsync();
            _futuresBarDataTimer.Start(async () => await InsertFuturesBarDataAsync());
            await WriteConsoleAsync(LogSourceType.MarketDataFeedService, "Futures Bar Data streaming started");
            _logger.LogInformationEvent($"{LogSourceType.MarketDataFeedService}", "futures bar data streaming started");
        }
        catch (Exception ex)
        {
            await WriteConsoleAsync(LogSourceType.MarketDataFeedService, ex);
            _logger.LogErrorEvent($"{LogSourceType.MarketDataFeedService}", ex, $"futures bar data streaming start failed due to {ex.GetErrorMessage()}");
        }
        return;

        async Task InsertFuturesBarDataAsync()
        {
            try
            {
                foreach (var o in e.Contracts!)
                {
                    var futuresTickData = await GetLastFuturesTickDataAsync(o.ContractId, e.ValueDate);
                    if (futuresTickData is not null)
                    {
                        switch (o.Symbol)
                        {
                            case "ES":
                            case "VX":
                                var futuresTradeSignal = await GetLastFuturesTradeSignalAsync(o.Symbol, e.ValueDate);
                                await _marketDataFeedCommandApi.InsertFuturesBarDataAsync(new FuturesBarDataReadModel(
                                    ContractId: o.ContractId,
                                    Symbol: o.Symbol,
                                    ValueDate: e.ValueDate,
                                    BarDate: DateTime.Now,
                                    BarRateType: BarRateType.Minute,
                                    BarValue: Convert.ToDecimal(futuresTickData?.Price),
                                    UpTrendTrigger: futuresTradeSignal?.UpTrendingTrigger ?? 0,
                                    DownTrendTrigger: futuresTradeSignal?.DownTrendingTrigger ?? 0
                                ));
                                await WriteConsoleAsync(LogSourceType.MarketDataFeedService, $"Inserted Futures Bar Data {o.ContractId}");
                                _logger.LogInformationEvent($"{LogSourceType.MarketDataFeedService}", $"inserted futures bar data {o.ContractId}");
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await WriteConsoleAsync(LogSourceType.MarketDataFeedService, ex);
                _logger.LogErrorEvent($"{LogSourceType.MarketDataFeedService}", ex, $"futures bar data insert failed due to {ex.GetErrorMessage()}");
            }
        }

        async Task<FuturesTickDataV2ReadModel?> GetLastFuturesTickDataAsync(string contractId, DateOnly valueDate)
        {
            var futuresTickData = default(FuturesTickDataV2ReadModel);
            var serviceResult = await _marketDataFeedQueryApi.GetLastFuturesTickDataAsync(contractId, e.ValueDate);
            if (serviceResult.Success && serviceResult.Value is not null)
                futuresTickData = serviceResult.Value;
            return futuresTickData;
        }

        async Task<FuturesTradeSignalV2ReadModel?> GetLastFuturesTradeSignalAsync(string symbol, DateOnly valueDate)
        {
            var futuresTradeSignal = default(FuturesTradeSignalV2ReadModel);
            var serviceResult = await _marketDataAnalyticsQueryApi.GetFuturesTradeSignalBySymbolAsync(symbol, valueDate);
            if (serviceResult.Success && serviceResult.Value is not null)
                futuresTradeSignal = serviceResult.Value;
            return futuresTradeSignal;
        }

    }

    /// <summary>
    /// stop streaming futures bar data
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task ExecuteAsync(FuturesBarDataStreamingStoppedEvent e)
    {
        try
        {
            _futuresBarDataTimer.Stop();
            _logger.LogInformationEvent($"{LogSourceType.MarketDataFeedService}","futures bar data streaming stopped");
        }
        catch (Exception ex)
        {
            await WriteConsoleAsync(LogSourceType.MarketDataFeedService, ex.GetErrorMessage());
            _logger.LogErrorEvent( $"{LogSourceType.MarketDataFeedService}",ex, $" futures bar data streaming stop failed due to {ex.GetErrorMessage()}");
        }
    }
}
