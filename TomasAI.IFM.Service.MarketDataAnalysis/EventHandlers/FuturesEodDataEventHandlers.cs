using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Shared.EventService;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Log;
using TomasAI.IFM.Shared.Log.ServiceApi;
using TomasAI.IFM.Shared.MarketData.ServiceApi;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Commands;
using TomasAI.IFM.Shared.MarketDataAnalytics.ServiceApi;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.ServiceApi;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.ViewModels;

namespace TomasAI.IFM.Service.MarketDataAnalytics.EventHandlers;

/// <summary>
/// Handles events related to the insertion of end-of-day (EOD) futures data and generates corresponding analytics
/// signals.
/// </summary>
/// <remarks>This class processes futures EOD data and generates intrinsic time indicators (ITI) signals based on
/// various market analytics. It integrates with multiple APIs to retrieve necessary data, calculate analytics, and
/// execute commands.</remarks>
/// <param name="commandApi"></param>
/// <param name="queryApi"></param>
/// <param name="marketDataFeedQueryApi"></param>
/// <param name="marketDataQueryApi"></param>
/// <param name="futuresItiTrendQueryApi"></param>
/// <param name="blackBoardService"></param>
/// <param name="statusConsoleWriter"></param>
/// <param name="logger"></param>
public class FuturesEodDataEventHandlers(
    IMarketDataAnalyticsCommandApi commandApi,
    IMarketDataAnalyticsQueryApi queryApi,
    IMarketDataFeedQueryApi marketDataFeedQueryApi,
    IMarketDataQueryApi marketDataQueryApi,
    IFuturesItiTrendQueryApi futuresItiTrendQueryApi,
    IBlackboardService blackBoardService,
    IStatusConsoleWriter statusConsoleWriter,
    ILogger<FuturesEodDataEventHandlers> logger) : BaseEventServiceHandler(statusConsoleWriter),
    IAsyncEventHandler<FuturesEodDataInsertedCompleteEvent, MarketDataAnalyticsService>
{
    IMarketDataAnalyticsCommandApi CommandApi { get; } = IsArgumentNull.Set(commandApi);
    IMarketDataAnalyticsQueryApi QueryApi { get; } = IsArgumentNull.Set(queryApi);
    IMarketDataFeedQueryApi MarketDataFeedQueryApi { get; } = IsArgumentNull.Set(marketDataFeedQueryApi);
    IMarketDataQueryApi MarketDataQueryApi { get; } = IsArgumentNull.Set(marketDataQueryApi);
    IFuturesItiTrendQueryApi FuturesItiTrendQueryApi { get; } = IsArgumentNull.Set(futuresItiTrendQueryApi);
    IBlackboardService BlackBoardService { get; } = IsArgumentNull.Set(blackBoardService);
    ILogger<FuturesEodDataEventHandlers> Logger { get; } = IsArgumentNull.Set(logger);

    /// <summary>
    /// create futures iti signal
    /// </summary>
    /// <param name="e">futures eod data inserted complete event</param>
    /// <returns></returns>
    public async Task ExecuteAsync(FuturesEodDataInsertedCompleteEvent e)
    {
        try
        {
            await GenerateFuturesItiSignalAsync(e.FuturesEodData.ContractId, e.FuturesEodData.ValueDate);
        }
        catch (Exception ex)
        {
            await WriteConsoleAsync(LogSourceType.MarketDataAnalytics, FuturesEodDataInsertedCompleteEvent.ErrorCode, ex.GetErrorMessage());
            Logger.LogError(ex.GetErrorMessage(), $"{LogSourceType.MarketDataAnalytics}: futures eod data inserted complete event - {e.FuturesEodData.ContractId} handler failed");
        }
    }

    /// <summary>
    /// generate futures iti signal based on futures eod data, RSI signal, VIX futures data, and MDI distribution.
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    async Task GenerateFuturesItiSignalAsync(string contractId, DateOnly valueDate)
    {
        var futuresEodData = await GetFuturesEodDataWithMovingAveragesAsync(contractId, valueDate);
        var futuresRsiSignal = await GetFuturesRsiSignalAsync(contractId, valueDate);
        var vixFuturesEodData = await GetVixFuturesEodDataAsync(valueDate);
        var futuresItiMDIDistribution = await GetFuturesItiMDIDistributionAsync(contractId, valueDate);
        if (futuresEodData is null || futuresRsiSignal is null || vixFuturesEodData is null )
            return;
        
        var symbol = await BlackBoardService.FuturesContractSymbol.GetAsync(contractId, GetFuturesContractSymbolAsync);
        var futuresItiSignal = await GetLastFuturesItiSignalAsync(contractId, valueDate);
        var predictedTrendDelta = futuresItiSignal is not null
            ? await GetPredictedTrendDeltaAsync(futuresItiSignal!)
            : 0.0;  
        var futuresItiTrendCoastLineCounters = await GetFuturesItiTrendCoastLineCountersAsync(contractId, valueDate, symbol, predictedTrendDelta);
        var intrinsicTime = DateTime.Now;
        var futuresPrice = futuresEodData.ClosePrice;
        var lambda = CalculateLambda(vixFuturesEodData?.ClosePrice ?? 0.0);
        await CommandApi.GenerateFuturesItiSignalAsync(
            futuresEodData.ContractId,
            futuresEodData.ValueDate,
            intrinsicTime,
            symbol,
            futuresPrice,
            lambda,
            futuresEodData.DailyPercentChange,
            futuresEodData.Mean,
            futuresEodData.DailyStdDev,
            futuresEodData.MarketDirectionIndicator,
            futuresRsiSignal.RSI,
            futuresRsiSignal.RSISlope,
            futuresEodData.FiftyDMA,
            futuresEodData.TwoHundredDMA,
            futuresItiMDIDistribution,
            predictedTrendDelta,
            futuresItiTrendCoastLineCounters);
        return;

        // Helper methods to retrieve data from various APIs
        async Task<FuturesEodDataV2ReadModel?> GetFuturesEodDataWithMovingAveragesAsync(string contractId, DateOnly valueDate)
        {
            var futuresEodData = default(FuturesEodDataV2ReadModel);
            var serviceResult = await MarketDataFeedQueryApi.GetFuturesEodDataAsync(contractId, valueDate);
            if (serviceResult.Success && serviceResult.Value is not null)
                futuresEodData = serviceResult.Value;
            return futuresEodData;
        }

        async Task<FuturesRsiSignalReadModel?> GetFuturesRsiSignalAsync(string contractId, DateOnly  valueDate)
        {
            var futuresRsiSignal = default(FuturesRsiSignalReadModel);
            var serviceResult = await QueryApi.GetFuturesRsiSignalAsync(contractId, valueDate);
            if (serviceResult.Success && serviceResult.Value is not null)
                futuresRsiSignal = serviceResult.Value;
            return futuresRsiSignal;
        }

        async Task<string> GetFuturesContractSymbolAsync(string contractId)
        {
            var symbol = string.Empty;
            var serviceResult = await MarketDataQueryApi.GetFuturesContractSymbolAsync(contractId);
            if (serviceResult.Success && serviceResult.Value is not null)
                symbol = serviceResult.Value;
            return symbol;
        }

        async Task<double> GetPredictedTrendDeltaAsync(FuturesItiSignalV2ReadModel e)
        {
            var trendData = new FuturesItiTrendDeltaDataReadModel(
                Symbol: symbol,
                ValueDate: e.ValueDate,
                Timestamp: e.IntrinsicTime,
                SequenceId: 0,
                TrendDelta: 0,
                TrendDirection: e.IntrinsicTimeTrend == IntrinsicTimeTrendType.UpTrend ? 1 : 0,
                TrendDirectionMode: GetTrendDirectionMode(e.IntrinsicTimeMode),
                FuturesPrice: Convert.ToSingle(e.IntrinsicPrice),
                TrendExtreme: Convert.ToSingle(e.TrendExtreme),
                FuturesRSI: Convert.ToSingle(e.FuturesRSI)
            );
            var predictedTrendDelta = 0.0;
            var serviceResult = await FuturesItiTrendQueryApi.GetPredictedTrendDeltaAsync(trendData);
            if (serviceResult.Success && serviceResult.Value is not null)
                predictedTrendDelta = serviceResult.Value.Value;    
            return predictedTrendDelta;

            static int GetTrendDirectionMode(IntrinsicTimeModeType e)
               => e switch
               {
                   IntrinsicTimeModeType.TrendDirectionChanged => 0,
                   IntrinsicTimeModeType.TrendExtremeChanged => 1,
                   IntrinsicTimeModeType.TrendReversalChanged => -1,
                   _ => 0
               };
        }

        async Task<FuturesItiMDIDistributionReadModel?> GetFuturesItiMDIDistributionAsync(string contractId, DateOnly valueDate)
        {
            var intrinsicTimeGroupId = 0;
            var serviceResultGrpId = await QueryApi.GetFuturesItiSignalAsync(contractId, valueDate);
            if (serviceResultGrpId is not null && serviceResultGrpId.Success)
                intrinsicTimeGroupId = serviceResultGrpId.Value.IntrinsicTimeGroupId;

            FuturesItiMDIDistributionReadModel mdiDistribution = default!;
            var serviceResult = await QueryApi.GetFuturesItiSignalMDIByTrendAsync(contractId, valueDate, intrinsicTimeGroupId);
            if (serviceResult is not null && serviceResult.Success)
                mdiDistribution = new FuturesItiMDIDistributionReadModel(serviceResult.Value);
            return mdiDistribution;
        }

        async Task<VixFuturesEodDataReadModel?> GetVixFuturesEodDataAsync(DateOnly valueDate)
        {
            List<FuturesContractV2ReadModel> futuresContracts = [];
            var serviceResult = await MarketDataQueryApi.GetCurrentlyTradedFuturesContractsAsync();
            if (serviceResult.Success && serviceResult.Value is not null)
                futuresContracts.AddRange(serviceResult.Value);
            var vixFuturesEodData = default(VixFuturesEodDataReadModel);
            var vixContractId = futuresContracts.FirstOrDefault(x => x.Symbol == "VX" && x.CurrentlyTraded)?.ContractId;
            if (vixContractId is not null)
            {
                var serviceResult2 = await MarketDataFeedQueryApi.GetLastVixFuturesEodDataAsync(vixContractId, valueDate);
                if (serviceResult2.Success && serviceResult2.Value is not null)
                    vixFuturesEodData = serviceResult2.Value;
            }
            return vixFuturesEodData;
        }

        async Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalAsync(string contractId, DateOnly valueDate)
        {
            FuturesItiSignalV2ReadModel? futuresItiSignal = default!;
            var serviceResult = await QueryApi.GetFuturesItiSignalAsync(contractId, valueDate);
            if (serviceResult.Success && serviceResult.Value is not null)
                futuresItiSignal  = serviceResult.Value;    
            return futuresItiSignal;
        }

        async Task<FuturesItiTrendCoastLineCountersReadModel?> GetFuturesItiTrendCoastLineCountersAsync(string contractId, DateOnly valueDate, string symbol, double predictedTrendDelta)
        {
            FuturesItiTrendCoastLineCountersReadModel? coastLineCounters = default!;
            var serviceResult = await FuturesItiTrendQueryApi.GetFuturesItiTrendCoastLineCountersAsync(contractId, valueDate, symbol, predictedTrendDelta);
            if (serviceResult.Success && serviceResult.Value is not null)
            {
                coastLineCounters = serviceResult.Value;
            }
            return coastLineCounters;
        }

        double CalculateLambda(double vixFuturesPrice)
        {
            var vixFuturesDailyVolatility = vixFuturesPrice / 15.7;
            var volatilityFactor = vixFuturesDailyVolatility > 1.0
                ? Math.Sqrt(vixFuturesDailyVolatility)
                : vixFuturesDailyVolatility;
            var minLambda = 1 / Math.PI * 2 * 0.003;
            return Math.Max(minLambda, 0.003 * volatilityFactor);
        }
    }
}
