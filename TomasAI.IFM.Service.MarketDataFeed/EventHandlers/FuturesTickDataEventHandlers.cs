using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventService;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.Log.ServiceApi;
using TomasAI.IFM.Shared.Log;
using TomasAI.IFM.Application.Blackboard;

namespace TomasAI.IFM.Service.MarketDataFeed.EventHandlers;

/// <summary>
/// FuturesEodDataInsertedEventHandler constructor
/// </summary>
/// <param name="marketDataApi"></param>
/// <param name="marketDataFeedCommandApi"></param>
/// <param name="marketDataFeedQueryApi"></param>
/// <param name="eventProducer"></param>
/// <param name="blackBoardService"></param>
/// <param name="statusConsoleWriter"></param>
/// <param name="logger"></param>
public class FuturesTickDataEventHandlers(
    IMarketDataApi marketDataApi,
    IMarketDataFeedCommandApi marketDataFeedCommandApi,
    IMarketDataFeedQueryApi marketDataFeedQueryApi,
    IMarketDataFeedEventProducer eventProducer,
    IBlackboardService blackBoardService,
    IStatusConsoleWriter statusConsoleWriter,
    ILogger<FuturesTickDataEventHandlers> logger) : BaseEventServiceHandler(statusConsoleWriter),
    IAsyncEventHandler<FuturesTickDataInsertedEvent, MarketDataFeedService>,
    IAsyncEventHandler<FuturesTickDataStreamingStartedEvent, MarketDataFeedService>,
    IAsyncEventHandler<FuturesTickDataStreamingStoppedEvent, MarketDataFeedService>,
    IAsyncEventHandler<FuturesTickPriceDataEvent, MarketDataFeedService>
{
    // private properties...
    IMarketDataApi MarketDataApi => IsArgumentNull.Set(marketDataApi);
    IMarketDataFeedCommandApi CommandApi => IsArgumentNull.Set(marketDataFeedCommandApi);
    IMarketDataFeedQueryApi QueryApi => IsArgumentNull.Set(marketDataFeedQueryApi);
    IMarketDataFeedEventProducer EventProducer => IsArgumentNull.Set(eventProducer);
    IBlackboardService BlackboardService => IsArgumentNull.Set(blackBoardService);
    ILogger<FuturesTickDataEventHandlers> Logger { get; } = IsArgumentNull.Set(logger);

    /// <summary>
    /// create futures eod data entry from tick data
    /// </summary>
    /// <param name="e">futures tick data inserted event</param>
    /// <returns></returns>
    public async Task ExecuteAsync(FuturesTickDataInsertedEvent e)
    {
        try
        {
            var valueDate = e.TickData.ValueDate;
            if (!e.Contract.Id.IsVixContract)
            {
                var eodDataToday = await GetFuturesEodDataAsync(e.Contract.ContractId, valueDate);
                if (eodDataToday is null)
                    return;
                var eodDataRange = await BlackboardService.FuturesEodDataRange.GetAsync(e.Contract.ContractId, valueDate, GetFuturesEodDataRangeAsync );
                var normCurveTbl = await BlackboardService.NormalCurveTable.GetAsync(valueDate, GetNormalCurveTableAsync!);
                var vixContractId = BlackboardService.VixFuturesContractId.Get(valueDate);
                var vixFuturesEodData = BlackboardService.VixFuturesEodData.Get(vixContractId!, valueDate);
                if (vixFuturesEodData is null)
                {
                    var serviceResult = await QueryApi.GetVixFuturesEodDataAsync(vixContractId!, valueDate);
                    if (serviceResult.Success && serviceResult.Value is not null && serviceResult.Value.Length  > 0)
                    {
                        vixFuturesEodData = serviceResult.Value;
                        BlackboardService.VixFuturesEodData.Set(vixFuturesEodData.First().ContractId, valueDate, vixFuturesEodData);
                        if (string.IsNullOrEmpty(vixContractId))
                            BlackboardService.VixFuturesContractId.Set(valueDate, vixFuturesEodData.First().ContractId);
                    }
                }

                // save futures eod data...
                if (eodDataToday.ClosePrice != e.TickData.Price)
                    await CommandApi.InsertFuturesEodDataAsync(valueDate, e.TickData, e.Contract, eodDataToday, eodDataRange, normCurveTbl, 20, vixFuturesEodData);
            }
            else
            {
                await CommandApi.InsertVixFuturesEodDataAsync(e.TickData);
                BlackboardService.VixFuturesContractId.Set(valueDate, e.TickData.ContractId);
            }
        }
        catch (Exception ex)
        {
            await WriteConsoleAsync(LogSourceType.MarketDataFeedService, 6009, ex.GetErrorMessage());
            Logger.LogError(ex.GetErrorMessage(), $"{LogSourceType.MarketDataFeedService}: futures eod data {e.Contract.ContractId} insert failed");
        }
        return;


        async Task<FuturesEodDataV2ReadModel?> GetFuturesEodDataAsync(string contractId, DateOnly valueDate)
        {
            var futureEodData = default(FuturesEodDataV2ReadModel);
            var serviceResult = await QueryApi.GetFuturesEodDataAsync(e.Contract.ContractId, valueDate);
            if (serviceResult.Success && serviceResult is not null)
                futureEodData = serviceResult.Value;
            return futureEodData;
        }

        async Task<FuturesEodDataV2ReadModel[]> GetFuturesEodDataRangeAsync(string contractId, DateOnly startDate, DateOnly endDate)
        {
            FuturesEodDataV2ReadModel[] futureEodDataRange = [];
            var serviceResult = await QueryApi.GetFuturesEodDataAsync(e.Contract.ContractId, startDate, endDate);
            if (serviceResult.Success && serviceResult.Value is not null)
                futureEodDataRange = serviceResult.Value;
            return futureEodDataRange;
        }

        async Task<Shared.MarketData.ViewModels.NormalCurveTableReadModel?> GetNormalCurveTableAsync()
        {
            var normalCurveTable = default(Shared.MarketData.ViewModels.NormalCurveTableReadModel);
            var serviceResult = await QueryApi.GetNormalCurveTableAsync();
            if (serviceResult.Success && serviceResult.Value is not null)
                normalCurveTable = serviceResult.Value;
            return normalCurveTable;
        }

    }

    /// <summary>
    /// start streaming futures tick data
    /// </summary>
    /// <param name="e">futures tick data streaming started event</param>
    /// <returns></returns>
    public async Task ExecuteAsync(FuturesTickDataStreamingStartedEvent e)
    {
        try
        {
            var streamId = MarketDataApi.StreamIds.Add(e.Contract.ContractId);
            if (streamId == -1)
                throw new InvalidOperationException($"{e.GetType().Name}: unable to create stream id from futures contract {e.Contract.ContractId} ");
            MarketDataApi.StartStreamingFuturesTickData(streamId, e.ValueDate, e.Contract);
            BlackboardService.FuturesTickDataStreamingParameter.Set(streamId, new FuturesTickDataStreamingParameter(streamId, e.ValueDate, e.Contract));
            await WriteConsoleAsync(LogSourceType.MarketDataFeedService, $"Futures {e.Contract.ContractId} streaming started");
            Logger.LogInformation($"{LogSourceType.MarketDataFeedService}: futures {e.Contract.ContractId} streaming started");
        }
        catch (Exception ex)
        {
            await WriteConsoleAsync(LogSourceType.MarketDataFeedService, 6003, ex.GetErrorMessage());
            Logger.LogError(ex, $"{LogSourceType.MarketDataFeedService}: futures {e.Contract.ContractId} streaming start failed");
        }
    }

    /// <summary>
    /// stop streaming futures tick data
    /// </summary>
    /// <param name="e">futures tick data streaming stopped event</param>
    /// <returns></returns>
    public async Task ExecuteAsync(FuturesTickDataStreamingStoppedEvent e)
    {
        try
        {
            var streamId = MarketDataApi.StreamIds[e.ContractId];
            if (streamId != -1 && MarketDataApi.StopStreamingFuturesTickData(streamId))
            {
                MarketDataApi.StreamIds.Remove(streamId);
                await WriteConsoleAsync(LogSourceType.MarketDataFeedService, $"futures tick data {e.ContractId} streaming stopped");
                Logger.LogInformation($"{LogSourceType.MarketDataFeedService}: futures tick data {e.ContractId} streaming stopped");
            }
        }
        catch (Exception ex)
        {
            await WriteConsoleAsync(LogSourceType.MarketDataFeedService, 6005, ex.GetErrorMessage());
            Logger.LogError(ex.GetErrorMessage(), $"{LogSourceType.MarketDataFeedService}: futures tick data {e.ContractId} streaming stop failed");
        }
    }

    /// <summary>
    /// save streamed futures tick data if price has changed since last futures tick data price has changed
    /// </summary>
    /// <param name="e">futures tick price data event</param>
    /// <returns></returns>
    public async Task ExecuteAsync(FuturesTickPriceDataEvent e)
    {
        var sp = BlackboardService.FuturesTickDataStreamingParameter.Get(e.RequestId);
        if (sp is null)
            return;
        //await _semaphoreSlim.WaitAsync();
        try
        {
            // save streamed futures tick data if price has changed...
            var lastFuturesTickData = BlackboardService.FuturesTickData.Get(sp.FuturesContract.ContractId, sp.ValueDate);
            if (lastFuturesTickData is null || lastFuturesTickData.Price != e.TickPriceData.Price || lastFuturesTickData.TickId != e.TickPriceData.TickTime)
            {
                var futuresTickData = new FuturesTickDataV2ReadModel(
                    ValueDate: sp.ValueDate,
                    ContractId: sp.FuturesContract.ContractId,
                    TickTime: TimeOnly.FromDateTime(e.TickPriceData.TickDate),
                    TickId: e.TickPriceData.TickTime,
                    Price: e.TickPriceData.Price,
                    Size: e.TickPriceData.Size);
                BlackboardService.FuturesTickData.Set(sp.FuturesContract.ContractId, sp.ValueDate, futuresTickData);
                await CommandApi.InsertFuturesTickDataAsync(sp.FuturesContract, futuresTickData);
            }
        }
        catch (Exception ex)
        {
            await WriteConsoleAsync(LogSourceType.MarketDataFeedService, 6004, ex.GetErrorMessage());
            Logger.LogError($"{LogSourceType.MarketDataFeedService}: futures {sp.FuturesContract.ContractId} streaming failed", ex);
        }
        /*
        finally
        {
            _semaphoreSlim.Release();
        }
        */
    }
}
