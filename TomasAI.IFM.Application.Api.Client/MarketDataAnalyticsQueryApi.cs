using TomasAI.IFM.Framework.Messaging;
using TomasAI.IFM.Shared.Application;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataAnalytics.ServiceApi;
using TomasAI.IFM.Shared.MarketDataAnalytics.Queries;
using TomasAI.IFM.Shared.MarketDataAnalytics.QueryParameters;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.MarketDataAnalytics; // For FuturesTradeSignalId, FuturesRsiSignalType

namespace TomasAI.IFM.Application.Api.Client;

/// <summary>
/// REST API client for MarketDataAnalytics queries that delegates to an <see cref="IQueryServiceApi"/>.
/// Mirrors the pattern used by <see cref="MarketDataFeedQueryApi"/>.
/// </summary>
/// <param name="querySvc"></param>
public class MarketDataAnalyticsQueryApi(IQueryServiceApi querySvc) : IMarketDataAnalyticsQueryApi
{
    readonly IQueryServiceApi _querySvc = IsArgumentNull.Set(querySvc);

    /// <summary>
    /// Gets the futures trade signal for a contract and value date.
    /// </summary>
    public async Task<ServiceResult<FuturesTradeSignalV2ReadModel>> GetFuturesTradeSignalAsync(string contractId, DateOnly valueDate)
    {
        var qryParam = new GetFuturesTradeSignalParameter(contractId, valueDate);
        return await _querySvc.PostQueryAsync<FuturesTradeSignalV2ReadModel>(MarketDataAnalyticsQueryUriPath.GetFuturesTradeSignal, qryParam, 1008);
    }

    /// <summary>
    /// Gets the last futures trade signal.
    /// </summary>
    public async Task<ServiceResult<FuturesTradeSignalV2ReadModel>> GetLastFuturesTradeSignalAsync()
    {
        var qryParam = new GetLastFuturesTradeSignalParameter();
        return await _querySvc.PostQueryAsync<FuturesTradeSignalV2ReadModel>(MarketDataAnalyticsQueryUriPath.GetLastFuturesTradeSignal, qryParam, 1009);
    }

    /// <summary>
    /// Gets the futures trade signal by symbol and value date.
    /// </summary>
    public async Task<ServiceResult<FuturesTradeSignalV2ReadModel>> GetFuturesTradeSignalBySymbolAsync(string symbol, DateOnly valueDate)
    {
        var qryParam = new GetFuturesTradeSignalBySymbolParameter(symbol, valueDate);
        return await _querySvc.ExecuteQueryAsync<FuturesTradeSignalV2ReadModel>(MarketDataAnalyticsQueryUriPath.GetFuturesTradeSignalBySymbol, qryParam, 1009);
    }

    /// <summary>
    /// Gets the futures trade signal IDs for a value date.
    /// </summary>
    public async Task<ServiceResult<FuturesTradeSignalId[]>> GetFuturesTradeSignalIdsAsync(DateOnly valueDate)
    {
        var qryParam = new GetFuturesTradeSignalIdsParameter(valueDate);
        return await _querySvc.PostQueryAsync<FuturesTradeSignalId[]>(MarketDataAnalyticsQueryUriPath.GetFuturesTradeSignalIds, qryParam, 1009);
    }

    /// <summary>
    /// Gets the futures RSI signal for a contract and value date (default signal type).
    /// </summary>
    public async Task<ServiceResult<FuturesRsiSignalReadModel>> GetFuturesRsiSignalAsync(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod, int periodLength)
    {
        var qryParam = new GetFuturesRsiSignalParameter(contractId, valueDate, timePeriod, periodLength);
        return await _querySvc.ExecuteQueryAsync<FuturesRsiSignalReadModel>(MarketDataAnalyticsQueryUriPath.GetFuturesRsiSignal, qryParam, GetFuturesRsiSignalQuery.ErrorId);
    }

   
    /// <summary>
    /// Gets the futures trend direction from RSI signal.
    /// </summary>
    public async Task<ServiceResult<FuturesTrendDirectionReadModel>> GetFuturesTrendDirectionFromRSISignalAsync(
        string contractId, DateOnly valueDate, DateTime timestamp, int loopbackInterval, DateTime startTime, DateTime endTime)
    {
        var qryParam = new GetFuturesTrendDirectionFromRSISignalParameter(contractId, valueDate, timestamp, loopbackInterval, startTime, endTime);
        return await _querySvc.ExecuteQueryAsync<FuturesTrendDirectionReadModel>(MarketDataAnalyticsQueryUriPath.GetFuturesTrendDirectionFromRSISignal, qryParam, 1011);
    }

    /// <summary>
    /// Gets the futures TDI signal for a contract and value date.
    /// </summary>
    public async Task<ServiceResult<FuturesTdiSignalReadModel>> GetFuturesTdiSignalAsync(string contractId, DateOnly valueDate)
    {
        var qryParam = new GetFuturesTdiSignalParameter(contractId, valueDate);
        return await _querySvc.PostQueryAsync<FuturesTdiSignalReadModel>(MarketDataAnalyticsQueryUriPath.GetFuturesTdiSignal, qryParam, 1021);
    }

    /// <summary>
    /// Gets the futures ITI signal for a contract and value date.
    /// </summary>
    public async Task<ServiceResult<FuturesItiSignalV2ReadModel>> GetFuturesItiSignalAsync(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod)
    {
        var qryParam = new GetFuturesItiSignalParameter(contractId, valueDate, timePeriod);
        return await _querySvc.ExecuteQueryAsync<FuturesItiSignalV2ReadModel>(MarketDataAnalyticsQueryUriPath.GetFuturesItiSignal, qryParam, 1021);
    }

    /// <summary>
    /// Gets the futures ITI trend direction changed signals for a contract and value date.
    /// </summary>
    public async Task<ServiceResult<FuturesItiSignalV2ReadModel[]>> GetFuturesItiTrendDirectionChangedSignalsAsync(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod)
    {
        var qryParam = new GetFuturesItiTrendDirectionChangedSignalsParameter(contractId, valueDate, timePeriod);
        return await _querySvc.ExecuteQueryAsync<FuturesItiSignalV2ReadModel[]>(MarketDataAnalyticsQueryUriPath.GetFuturesItiTrendDirectionChangedSignals, qryParam, 1022);
    }

    /// <summary>
    /// Gets the futures ITI signal data for a contract and value date.
    /// </summary>
    public async Task<ServiceResult<FuturesItiSignalDataReadModel>> GetFuturesItiSignalDataAsync(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod)
    {
        var qryParam = new GetFuturesItiSignalDataParameter(contractId, valueDate, timePeriod);
        return await _querySvc.ExecuteQueryAsync<FuturesItiSignalDataReadModel>(MarketDataAnalyticsQueryUriPath.GetFuturesItiSignalData, qryParam, 1022);
    }

    /// <summary>
    /// Gets the futures ITI MDI distribution for a contract and value date.
    /// </summary>
    public async Task<ServiceResult<FuturesItiMDIDistributionReadModel>> GetFuturesItiMDIDistributionAsync(string contractId, DateOnly valueDate)
    {
        var qryParam = new GetFuturesItiMDIDistributionParameter(contractId, valueDate);
        return await _querySvc.ExecuteQueryAsync<FuturesItiMDIDistributionReadModel>(MarketDataAnalyticsQueryUriPath.GetFuturesItiMDIDistribution, qryParam, 1030);
    }

    /// <summary>
    /// Gets the futures ITI MDI distribution by trend for a contract and value date.
    /// </summary>
    public async Task<ServiceResult<FuturesItiMDIDistributionReadModel>> GetFuturesItiMDIDistributionByTrendAsync(string contractId, DateOnly valueDate)
    {
        var qryParam = new GetFuturesItiMDIDistributionByTrendParameter(contractId, valueDate);
        return await _querySvc.ExecuteQueryAsync<FuturesItiMDIDistributionReadModel>(MarketDataAnalyticsQueryUriPath.GetFuturesItiMDIDistributionByTrend, qryParam, 1030);
    }

    /// <summary>
    /// Gets the futures ITI signal MDI for a contract and value date.
    /// </summary>
    public async Task<ServiceResult<FuturesItiSignalMDIV2ReadModel[]>> GetFuturesItiSignalMDIAsync(string contractId, DateOnly valueDate)
    {
        var qryParam = new GetFuturesItiSignalMDIParameter(contractId, valueDate);
        return await _querySvc.ExecuteQueryAsync<FuturesItiSignalMDIV2ReadModel[]>(MarketDataAnalyticsQueryUriPath.GetFuturesItiSignalMDI, qryParam, 1024);
    }

    /// <summary>
    /// Gets the futures ITI signal MDI by trend for a contract, value date, and group ID.
    /// </summary>
    public async Task<ServiceResult<FuturesItiSignalMDIV2ReadModel[]>> GetFuturesItiSignalMDIByTrendAsync(string contractId, DateOnly valueDate, int groupId)
    {
        var qryParam = new GetFuturesItiSignalMDIByTrendParameter(contractId, valueDate, groupId);
        return await _querySvc.ExecuteQueryAsync<FuturesItiSignalMDIV2ReadModel[]>(MarketDataAnalyticsQueryUriPath.GetFuturesItiSignalMDIByTrend, qryParam, 1024);
    }

    /// <summary>
    /// Gets the futures ATR signal for a contract and value date.
    /// </summary>
    public async Task<ServiceResult<FuturesAtrSignalReadModel>> GetFuturesAtrSignalAsync(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod, int periodLength)
    {
        var qryParam = new GetFuturesAtrSignalParameter(contractId, valueDate, timePeriod, periodLength);
        return await _querySvc.ExecuteQueryAsync<FuturesAtrSignalReadModel>(MarketDataAnalyticsQueryUriPath.GetFuturesAtrSignal, qryParam, GetFuturesAtrSignalQuery.ErrorId);
    }

    /// <summary>
    /// Gets the futures ADX signal for a contract and value date.
    /// </summary>
    public async Task<ServiceResult<FuturesAdxSignalReadModel>> GetFuturesAdxSignalAsync(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod, int periodLength)
    {
        var qryParam = new GetFuturesAdxSignalParameter(contractId, valueDate, timePeriod, periodLength);
        return await _querySvc.ExecuteQueryAsync<FuturesAdxSignalReadModel>(MarketDataAnalyticsQueryUriPath.GetFuturesAdxSignal, qryParam, GetFuturesAdxSignalQuery.ErrorId);
    }

    /// <summary>
    /// Gets the futures MACD signal for a contract and value date.
    /// </summary>
    public async Task<ServiceResult<FuturesMacdSignalReadModel>> GetFuturesMacdSignalAsync(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod, int periodLength)
    {
        var qryParam = new GetFuturesMacdSignalParameter(contractId, valueDate, timePeriod, periodLength);
        return await _querySvc.ExecuteQueryAsync<FuturesMacdSignalReadModel>(MarketDataAnalyticsQueryUriPath.GetFuturesMacdSignal, qryParam, GetFuturesMacdSignalQuery.ErrorId);
    }
}
