using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.MarketDataAnalytics.Queries;
using TomasAI.IFM.Shared.MarketDataAnalytics.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Framework.Messaging;

namespace TomasAI.IFM.Application.Query.Client;

public class MarketDataAnalyticsQueryApi(IQueryService querySvc) : IMarketDataAnalyticsQueryApi
{
    readonly IQueryService _querySvc = IsArgumentNull.Set(querySvc);
    readonly string _controller = "MarketDataAnalytics";

    /// <summary>
    /// return futures trade signal
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<ServiceResult<FuturesTradeSignalV2ReadModel>> GetFuturesTradeSignalAsync(string contractId, DateOnly valueDate)
        => await _querySvc.ExecuteApiQueryAsync(new GetFuturesTradeSignalQuery( contractId,  valueDate ), _controller);

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public async Task<ServiceResult<FuturesTradeSignalV2ReadModel>> GetLastFuturesTradeSignalAsync()
        => await _querySvc.ExecuteApiQueryAsync(new GetLastFuturesTradeSignalQuery(), _controller);

    /// <summary>
    /// return futures trade signal by symbol
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<ServiceResult<FuturesTradeSignalV2ReadModel>> GetFuturesTradeSignalBySymbolAsync(string symbol, DateOnly valueDate)
        => await _querySvc.ExecuteApiQueryAsync(new GetFuturesTradeSignalBySymbolQuery( symbol, valueDate ), _controller);

    /// <summary>
    /// return futures trade signal ids
    /// </summary>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<ServiceResult<FuturesTradeSignalId[]>> GetFuturesTradeSignalIdsAsync(DateOnly valueDate)
        => await _querySvc.ExecuteApiQueryAsync(new GetFuturesTradeSignalIdsQuery (valueDate ), _controller);

    /// <summary>
    /// return futures rsi signal by default thirty seconds signal type
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<ServiceResult<FuturesRsiSignalReadModel>> GetFuturesRsiSignalAsync(string contractId, DateOnly valueDate)
        => await _querySvc.ExecuteApiQueryAsync(new GetFuturesRsiSignalQuery(contractId,  valueDate, FuturesRsiSignalType.OneMinute ), _controller);

    /// <summary>
    /// return futures rsi signal 
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <param name="signalType"></param>
    /// <returns></returns>
    public async Task<ServiceResult<FuturesRsiSignalReadModel>> GetFuturesRsiSignalAsync(string contractId, DateOnly valueDate, FuturesRsiSignalType signalType)
        => await _querySvc.ExecuteApiQueryAsync(new GetFuturesRsiSignalQuery ( contractId, valueDate, signalType), _controller);

    /// <summary>
    /// return futures trend direction from futures rsi signal
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <param name="timestamp"></param>
    /// <param name="lookbackInterval"></param>
    /// <param name="startTime"></param>
    /// <param name="endTime"></param>
    /// <returns></returns>
    public async Task<ServiceResult<FuturesTrendDirectionReadModel>> GetFuturesTrendDirectionFromRSISignalAsync(
        string contractId, DateOnly valueDate, DateTime timestamp, int lookbackInterval, DateTime startTime, DateTime endTime)
        => await _querySvc.ExecuteApiQueryAsync(new GetFuturesTrendDirectionFromRSISignalQuery (
             contractId,  valueDate,  timestamp, lookbackInterval, startTime, endTime ), _controller);

    /// <summary>
    /// return futures tdi signal 
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<ServiceResult<FuturesTdiSignalReadModel>> GetFuturesTdiSignalAsync(string contractId, DateOnly valueDate)
        => await _querySvc.ExecuteApiQueryAsync(new GetFuturesTdiSignalQuery (contractId, valueDate ),  _controller);

    /// <summary>
    /// return futures iti signal 
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<ServiceResult<FuturesItiSignalV2ReadModel>> GetFuturesItiSignalAsync(string contractId, DateOnly valueDate)
        => await _querySvc.ExecuteApiQueryAsync(new GetFuturesItiSignalQuery(contractId, valueDate), _controller);

    /// <summary>
    /// return futures iti trend direction changed signals
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<ServiceResult<FuturesItiSignalV2ReadModel[]>> GetFuturesItiTrendDirectionChangedSignalsAsync(string contractId, DateOnly valueDate)
        => await _querySvc.ExecuteApiQueryAsync(new GetFuturesItiTrendDirectionChangedSignalsQuery (contractId, valueDate ), _controller);

    /// <summary>
    /// return futures iti signal data
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<ServiceResult<FuturesItiSignalDataReadModel>> GetFuturesItiSignalDataAsync(string contractId, DateOnly valueDate)
        => await _querySvc.ExecuteApiQueryAsync(new GetFuturesItiSignalDataQuery (contractId, valueDate ), _controller);

    /// <summary>
    /// return futures iti mdi distribution
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<ServiceResult<FuturesItiMDIDistributionReadModel>> GetFuturesItiMDIDistributionAsync(string contractId, DateOnly valueDate)
        => await _querySvc.ExecuteApiQueryAsync(new GetFuturesItiMDIDistributionQuery( contractId,valueDate ), _controller);

    /// <summary>
    /// return futures iti mdi distribution by trend
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<ServiceResult<FuturesItiMDIDistributionReadModel>> GetFuturesItiMDIDistributionByTrendAsync(string contractId, DateOnly valueDate)
        => await _querySvc.ExecuteApiQueryAsync(new GetFuturesItiMDIDistributionByTrendQuery (contractId, valueDate), _controller);

    /// <summary>
    /// return futures iti signal mdi
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<ServiceResult<FuturesItiSignalMDIV2ReadModel[]>> GetFuturesItiSignalMDIAsync(string contractId, DateOnly valueDate)
        => await _querySvc.ExecuteApiQueryAsync(new GetFuturesItiSignalMDIQuery (contractId, valueDate ), _controller);

    /// <summary>
    /// return futures iti signal mdi by trend
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<ServiceResult<FuturesItiSignalMDIV2ReadModel[]>> GetFuturesItiSignalMDIByTrendAsync(string contractId, DateOnly valueDate, int groupId)
        => await _querySvc.ExecuteApiQueryAsync(new GetFuturesItiSignalMDIByTrendQuery (contractId, valueDate, groupId), _controller);


}
