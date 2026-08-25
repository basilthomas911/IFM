using TomasAI.IFM.Framework.Messaging;
using TomasAI.IFM.Shared.Application;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared; // For FuturesTradeSignalId, FuturesRsiSignalType

namespace TomasAI.IFM.Application.Api.Client;

/// <summary>
/// REST API client for MarketDataAnalytics queries that delegates to an <see cref="IQueryServiceApi"/>.
/// Mirrors the pattern used by <see cref="MarketDataFeedQueryApi"/>.
/// </summary>
/// <param name="querySvc"></param>
public class MarketDataAnalyticsQueryApi(IQueryServiceApi querySvc) : IMarketDataAnalyticsQueryApi
{
    readonly IQueryServiceApi _querySvc = IsArgumentNull.Set(querySvc);

    public async Task<ServiceResult<MarketOutlookSnapshotReadModel>> GetMarketOutlookSnapshotAsync(
        string contractId,
        DateOnly valueDate)
    {
        var parameter = new GetMarketOutlookSnapshotParameter(contractId, valueDate);
        return await _querySvc.ExecuteQueryAsync<MarketOutlookSnapshotReadModel>(
            MarketDataAnalyticsQueryUriPath.GetMarketOutlookSnapshot,
            parameter,
            GetMarketOutlookSnapshotQuery.ErrorId);
    }

    /// <summary>
    /// Gets the futures trade signal for a contract and value date.
    /// </summary>
    public async Task<ServiceResult<FuturesTradeSignalV2ReadModel>> GetFuturesTradeSignalAsync(string contractId, DateOnly valueDate)
    {
        var qryParam = new GetFuturesTradeSignalParameter(contractId, valueDate);
        return await _querySvc.ExecuteQueryAsync<FuturesTradeSignalV2ReadModel>(
            MarketDataAnalyticsQueryUriPath.GetFuturesTradeSignal, qryParam, GetFuturesTradeSignalQuery.ErrorId);
    }

    /// <summary>
    /// Gets the last futures trade signal.
    /// </summary>
    public async Task<ServiceResult<FuturesTradeSignalV2ReadModel>> GetLastFuturesTradeSignalAsync()
    {
        var qryParam = new GetLastFuturesTradeSignalParameter();
        return await _querySvc.ExecuteQueryAsync<FuturesTradeSignalV2ReadModel>(
            MarketDataAnalyticsQueryUriPath.GetLastFuturesTradeSignal, qryParam, GetLastFuturesTradeSignalQuery.ErrorId);
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
        return await _querySvc.ExecuteQueryAsync<FuturesTradeSignalId[]>(
            MarketDataAnalyticsQueryUriPath.GetFuturesTradeSignalIds, qryParam, GetFuturesTradeSignalIdsQuery.ErrorId);
    }

    /// <summary>
    /// Gets the futures RSI signal for a contract and value date (default signal type).
    /// </summary>
    public async Task<ServiceResult<FuturesRsiSignalReadModel>> GetFuturesRsiSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength)
    {
        var qryParam = new GetFuturesRsiSignalParameter(contractId, valueDate, timePeriod, periodLength);
        return await _querySvc.ExecuteQueryAsync<FuturesRsiSignalReadModel>(MarketDataAnalyticsQueryUriPath.GetFuturesRsiSignal, qryParam, GetFuturesRsiSignalQuery.ErrorId);
    }

   
    /// <summary>
    /// Gets the futures trend direction from RSI signal.
    /// </summary>
    public async Task<ServiceResult<FuturesTrendDirectionReadModel>> GetFuturesTrendDirectionFromRSISignalAsync(
        string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength,
        DateTime timestamp, int loopbackInterval, DateTime startTime, DateTime endTime)
    {
        var qryParam = new GetFuturesTrendDirectionFromRSISignalParameter(
            contractId, valueDate, timePeriod, periodLength, timestamp, loopbackInterval, startTime, endTime);
        return await _querySvc.ExecuteQueryAsync<FuturesTrendDirectionReadModel>(MarketDataAnalyticsQueryUriPath.GetFuturesTrendDirectionFromRSISignal, qryParam, 1011);
    }

    /// <summary>
    /// Gets the futures TDI signal for a contract and value date.
    /// </summary>
    public async Task<ServiceResult<FuturesTdiSignalReadModel>> GetFuturesTdiSignalAsync(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod = TimeFrameType.OneMinute,
        string configurationId = FuturesTdiConfiguration.StandardConfigurationId)
    {
        var qryParam = new GetFuturesTdiSignalParameter(contractId, valueDate, timePeriod, configurationId);
        return await _querySvc.ExecuteQueryAsync<FuturesTdiSignalReadModel>(
            MarketDataAnalyticsQueryUriPath.GetFuturesTdiSignal,
            qryParam,
            GetFuturesTdiSignalQuery.ErrorId);
    }

    /// <summary>
    /// Gets the futures ITI signal for a contract and value date.
    /// </summary>
    public async Task<ServiceResult<FuturesItiSignalV2ReadModel>> GetFuturesItiSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod)
    {
        var qryParam = new GetFuturesItiSignalParameter(contractId, valueDate, timePeriod);
        return await _querySvc.ExecuteQueryAsync<FuturesItiSignalV2ReadModel>(MarketDataAnalyticsQueryUriPath.GetFuturesItiSignal, qryParam, 1021);
    }

    /// <summary>Gets the complete Futures ITI signal history represented by a display timeframe.</summary>
    public async Task<ServiceResult<FuturesItiSignalV2ReadModel[]>> GetFuturesItiSignalHistoryAsync(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod)
    {
        var qryParam = new GetFuturesItiSignalHistoryParameter(contractId, valueDate, timePeriod);
        return await _querySvc.ExecuteQueryAsync<FuturesItiSignalV2ReadModel[]>(
            MarketDataAnalyticsQueryUriPath.GetFuturesItiSignalHistory,
            qryParam,
            GetFuturesItiSignalHistoryQuery.ErrorId);
    }

    /// <summary>
    /// Gets the futures ITI trend direction changed signals for a contract and value date.
    /// </summary>
    public async Task<ServiceResult<FuturesItiSignalV2ReadModel[]>> GetFuturesItiTrendDirectionChangedSignalsAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod)
    {
        var qryParam = new GetFuturesItiTrendDirectionChangedSignalsParameter(contractId, valueDate, timePeriod);
        return await _querySvc.ExecuteQueryAsync<FuturesItiSignalV2ReadModel[]>(MarketDataAnalyticsQueryUriPath.GetFuturesItiTrendDirectionChangedSignals, qryParam, 1022);
    }

    /// <summary>
    /// Gets the futures ITI signal data for a contract and value date.
    /// </summary>
    public async Task<ServiceResult<FuturesItiSignalDataReadModel>> GetFuturesItiSignalDataAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod)
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
    public async Task<ServiceResult<FuturesAtrSignalReadModel>> GetFuturesAtrSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength)
    {
        var qryParam = new GetFuturesAtrSignalParameter(contractId, valueDate, timePeriod, periodLength);
        return await _querySvc.ExecuteQueryAsync<FuturesAtrSignalReadModel>(MarketDataAnalyticsQueryUriPath.GetFuturesAtrSignal, qryParam, GetFuturesAtrSignalQuery.ErrorId);
    }

    /// <summary>
    /// Gets the futures ADX signal for a contract and value date.
    /// </summary>
    public async Task<ServiceResult<FuturesAdxSignalReadModel>> GetFuturesAdxSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength)
    {
        var qryParam = new GetFuturesAdxSignalParameter(contractId, valueDate, timePeriod, periodLength);
        return await _querySvc.ExecuteQueryAsync<FuturesAdxSignalReadModel>(MarketDataAnalyticsQueryUriPath.GetFuturesAdxSignal, qryParam, GetFuturesAdxSignalQuery.ErrorId);
    }

    /// <summary>
    /// Gets the futures MACD signal for a contract and value date.
    /// </summary>
    public async Task<ServiceResult<FuturesMacdSignalReadModel>> GetFuturesMacdSignalAsync(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        int signalEmaPeriod = FuturesMacdConfiguration.ConventionalSignalEmaPeriod,
        int fastEmaPeriod = FuturesMacdConfiguration.ConventionalFastEmaPeriod,
        int slowEmaPeriod = FuturesMacdConfiguration.ConventionalSlowEmaPeriod)
    {
        var qryParam = new GetFuturesMacdSignalParameter(
            contractId,
            valueDate,
            timePeriod,
            signalEmaPeriod,
            fastEmaPeriod,
            slowEmaPeriod);
        return await _querySvc.ExecuteQueryAsync<FuturesMacdSignalReadModel>(MarketDataAnalyticsQueryUriPath.GetFuturesMacdSignal, qryParam, GetFuturesMacdSignalQuery.ErrorId);
    }
}
