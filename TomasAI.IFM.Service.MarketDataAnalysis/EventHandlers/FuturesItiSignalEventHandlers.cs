using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventService;
using TomasAI.IFM.Shared.MarketDataAnalytics.ServiceApi;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Log.ServiceApi;
using TomasAI.IFM.Shared.Log;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketData.ServiceApi;

namespace TomasAI.IFM.Service.MarketDataAnalytics.EventHandlers;

/// <summary>
/// futures eod data inserted complete event handler
/// </summary>
/// <remarks>
/// FuturesEodDataInsertedEventHandler constructor
/// </remarks>
/// <param name="commandApi"></param>
/// <param name="queryApi"></param>
/// <param name="marketDataFeedQueryApi"></param>
/// <param name="blackBoardService"></param>
/// <param name="statusConsoleWriter"></param>
/// <param name="logger"></param>
public class FuturesItiSignalEventHandlers(
    IMarketDataAnalyticsCommandApi commandApi,
    IMarketDataAnalyticsQueryApi queryApi,
    IMarketDataFeedQueryApi marketDataFeedQueryApi,
    IMarketDataQueryApi marketDataQueryApi,
    IStatusConsoleWriter statusConsoleWriter,
    ILogger<FuturesItiSignalEventHandlers> logger) : BaseEventServiceHandler(statusConsoleWriter),
    IAsyncEventHandler<FuturesItiSignalGeneratedCompleteEvent, MarketDataAnalyticsService>
{
    IMarketDataAnalyticsCommandApi CommandApi { get; } = IsArgumentNull.Set(commandApi);
    IMarketDataAnalyticsQueryApi QueryApi { get; } = IsArgumentNull.Set(queryApi);
    IMarketDataFeedQueryApi MarketDataFeedQueryApi { get; } = IsArgumentNull.Set(marketDataFeedQueryApi);
    IMarketDataQueryApi MarketDataQueryApi { get; } = IsArgumentNull.Set(marketDataQueryApi);
    ILogger<FuturesItiSignalEventHandlers> Logger { get; } = IsArgumentNull.Set(logger);

    /// <summary>
    /// create futures trade signal when futures iti signal has been generated
    /// </summary>
    /// <param name="e">futures eod data inserted complete event</param>
    /// <returns></returns>
    public async Task ExecuteAsync(FuturesItiSignalGeneratedCompleteEvent e)
    {
        try
        {
            await GenerateFuturesTradeSignalAsync(e.EntityId!.ContractId, e.EntityId.ValueDate);
        }
        catch (Exception ex)
        {
            await WriteConsoleAsync(LogSourceType.MarketDataAnalytics, e.ErrorCode, ex.GetErrorMessage());
            Logger.LogError(ex.GetErrorMessage(), $"{LogSourceType.MarketDataAnalytics}: {e.GetType().Name} - {e.EntityId.ContractId} handler failed");
        }

    }

    /// <summary>
    /// generate futures trade signal
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    async Task GenerateFuturesTradeSignalAsync(string contractId, DateOnly valueDate)
    {
        var futuresEodData = await GetFuturesEodDataWithMovingAveragesAsync(contractId, valueDate);
        var futuresRsiSignal = await GetFuturesRsiSignalAsync(contractId, valueDate);
        var futuresTdiSignal = await GetFuturesTdiSignalAsync(contractId, valueDate);
        var futuresItiSignalData = await GetFuturesItiSignalDataAsync(contractId, valueDate);
        var vixFuturesPrice = (await GetVixFuturesEodDataAsync(valueDate))?.ClosePrice ?? 0.0;
        await CommandApi.UpdateFuturesTradeSignalAsync(futuresEodData!, futuresRsiSignal!, futuresTdiSignal!, futuresItiSignalData!, vixFuturesPrice);

        async Task<FuturesEodDataV2ReadModel?> GetFuturesEodDataWithMovingAveragesAsync(string contractId, DateOnly valueDate)
        {
            var futuresEodData = default(FuturesEodDataV2ReadModel);
            var serviceResult = await MarketDataFeedQueryApi.GetFuturesEodDataAsync(contractId, valueDate);
            if (serviceResult.Success && serviceResult.Value is not null)
                futuresEodData = serviceResult.Value;
            return futuresEodData;
        }

        async Task<FuturesRsiSignalReadModel?> GetFuturesRsiSignalAsync(string contractId, DateOnly valueDate)
        {
            var futuresRsiSignal = default(FuturesRsiSignalReadModel);
            var serviceResult = await QueryApi.GetFuturesRsiSignalAsync(contractId, valueDate);
            if (serviceResult.Success && serviceResult.Value is not null)
                futuresRsiSignal = serviceResult.Value;
            return futuresRsiSignal;
        }

        async Task<FuturesTdiSignalReadModel?> GetFuturesTdiSignalAsync(string contractId, DateOnly valueDate)
        {
            var futuresTdiSignal = default(FuturesTdiSignalReadModel);
            var serviceResult = await QueryApi.GetFuturesTdiSignalAsync(contractId, valueDate);
            if (serviceResult.Success && serviceResult.Value is not null)
                futuresTdiSignal = serviceResult.Value;
            return futuresTdiSignal;
        }

        async Task<FuturesItiSignalDataReadModel?> GetFuturesItiSignalDataAsync(string contractId, DateOnly valueDate)
        {
            var futuresItiSignal = default(FuturesItiSignalDataReadModel);
            var serviceResult = await QueryApi.GetFuturesItiSignalDataAsync(contractId, valueDate);
            if (serviceResult.Success && serviceResult.Value is not null)
                futuresItiSignal = serviceResult.Value;
            return futuresItiSignal;
        }

        async Task<VixFuturesEodDataReadModel?> GetVixFuturesEodDataAsync( DateOnly valueDate)
        {
            var vixFuturesEodData = default(VixFuturesEodDataReadModel);
            List<FuturesContractV2ReadModel> futuresContracts = [];
            var serviceResult = await MarketDataQueryApi.GetCurrentlyTradedFuturesContractsAsync();
            if (serviceResult.Success && serviceResult.Value is not null)
            {
                futuresContracts.AddRange(serviceResult.Value);
                var vixContractId = futuresContracts.FirstOrDefault(x => x.Symbol == "VX" && x.CurrentlyTraded)?.ContractId;
                if (vixContractId is not null)
                {
                    var serviceResult2 = await MarketDataFeedQueryApi.GetLastVixFuturesEodDataAsync(vixContractId, valueDate);
                    if (serviceResult2.Success && serviceResult2.Value is not null)
                        vixFuturesEodData = serviceResult2.Value;
                }
            }
            return vixFuturesEodData;
        }

    }

}
