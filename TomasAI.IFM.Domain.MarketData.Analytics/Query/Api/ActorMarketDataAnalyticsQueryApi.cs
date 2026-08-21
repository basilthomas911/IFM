using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Query.Api;

/// <summary>
/// Provides direct, in-process Market Data Analytics queries without actor messaging.
/// </summary>
/// <remarks>
/// Signal and distribution data is read through <see cref="IDbContextFactory.MarketDataDb"/>. Every public
/// operation owns its exception handling and returns a typed service result using the corresponding query
/// error identifier. The implementation does not capture actor context and may be registered as a singleton.
/// </remarks>
public sealed partial class ActorMarketDataAnalyticsQueryApi(IDbContextFactory dbFactory)
    : IActorMarketDataAnalyticsQueryApi
{
    readonly IDbContextFactory _dbFactory = IsArgumentNull.Set(dbFactory);

    public async Task<ServiceResult<MarketOutlookSnapshotReadModel>> GetMarketOutlookSnapshotAsync(
        string contractId,
        DateOnly valueDate)
    {
        try
        {
            var result = await _dbFactory.MarketDataDb
                .GetMarketOutlookSnapshotAsync(contractId, valueDate)
                .ConfigureAwait(false);
            return new ServiceOk<MarketOutlookSnapshotReadModel>(result!);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<MarketOutlookSnapshotReadModel>(
                GetMarketOutlookSnapshotQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets futures trade signal.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FuturesTradeSignalV2ReadModel>> GetFuturesTradeSignalAsync(
        string contractId, DateOnly valueDate)
    {
        try
        {
            FuturesTradeSignalV2ReadModel result =
                (await _dbFactory.MarketDataDb.GetLastFuturesTradeSignalAsync(contractId, valueDate))!;
            return new ServiceOk<FuturesTradeSignalV2ReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesTradeSignalV2ReadModel>(
                GetFuturesTradeSignalQuery.ErrorId,
                ex.Message);
        }
    }

    /// <summary>
    /// Gets last futures trade signal.
    /// </summary>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FuturesTradeSignalV2ReadModel>> GetLastFuturesTradeSignalAsync()
    {
        try
        {
            FuturesTradeSignalV2ReadModel result =
                (await _dbFactory.MarketDataDb.GetLastFuturesTradeSignalAsync())!;
            return new ServiceOk<FuturesTradeSignalV2ReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesTradeSignalV2ReadModel>(
                GetLastFuturesTradeSignalQuery.ErrorId,
                ex.Message);
        }
    }

    /// <summary>
    /// Gets futures trade signal by symbol.
    /// </summary>
    /// <param name="symbol">The market symbol.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FuturesTradeSignalV2ReadModel>> GetFuturesTradeSignalBySymbolAsync(
        string symbol, DateOnly valueDate)
    {
        try
        {
            FuturesTradeSignalV2ReadModel result =
                (await _dbFactory.MarketDataDb.GetLastFuturesTradeSignalBySymbolAsync(symbol, valueDate))!;
            return new ServiceOk<FuturesTradeSignalV2ReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesTradeSignalV2ReadModel>(
                GetFuturesTradeSignalBySymbolQuery.ErrorId,
                ex.Message);
        }
    }

    /// <summary>
    /// Gets futures trade signal IDs.
    /// </summary>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FuturesTradeSignalId[]>> GetFuturesTradeSignalIdsAsync(DateOnly valueDate)
    {
        try
        {
            FuturesTradeSignalId[] result =
                [.. await _dbFactory.MarketDataDb.GetFuturesTradeSignalIdByValueDateAsync(valueDate)];
            return new ServiceOk<FuturesTradeSignalId[]>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesTradeSignalId[]>(
                GetFuturesTradeSignalIdsQuery.ErrorId,
                ex.Message);
        }
    }

    /// <summary>
    /// Gets futures RSI signal.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <param name="timePeriod">The signal time-frame type.</param>
    /// <param name="periodLength">The indicator period length.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FuturesRsiSignalReadModel>> GetFuturesRsiSignalAsync(
        string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength)
    {
        try
        {
            FuturesRsiSignalReadModel result =
                (await _dbFactory.MarketDataDb.GetLastFuturesRsiSignalAsync(
                    contractId,
                    valueDate,
                    timePeriod,
                    periodLength))!;
            return new ServiceOk<FuturesRsiSignalReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesRsiSignalReadModel>(GetFuturesRsiSignalQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets futures RSI daily signal.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <param name="timePeriod">The signal time-frame type.</param>
    /// <param name="periodLength">The indicator period length.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FuturesRsiSignalReadModel>> GetFuturesRsiDailySignalAsync(
        string contractId, TimeFrameType timePeriod, int periodLength)
    {
        try
        {
            FuturesRsiSignalReadModel result =
                (await _dbFactory.MarketDataDb.GetLastFuturesRsiDailySignalAsync(
                    contractId,
                    timePeriod,
                    periodLength))!;
            return new ServiceOk<FuturesRsiSignalReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesRsiSignalReadModel>(
                GetFuturesRsiDailySignalQuery.ErrorId,
                ex.Message);
        }
    }

    /// <summary>
    /// Gets futures trend direction from RSI signal.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <param name="timestamp">The signal timestamp.</param>
    /// <param name="loopbackInterval">The number of intervals to inspect.</param>
    /// <param name="startTime">The start of the intraday time window.</param>
    /// <param name="endTime">The end of the intraday time window.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FuturesTrendDirectionReadModel>> GetFuturesTrendDirectionFromRSISignalAsync(
        string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength,
        DateTime timestamp, int loopbackInterval,
        DateTime startTime, DateTime endTime)
    {
        try
        {
            FuturesTrendDirectionReadModel result =
                await _dbFactory.MarketDataDb.GetFuturesTrendDirectionFromRSISignalAsync(
                    contractId,
                    valueDate,
                    timePeriod,
                    periodLength,
                    timestamp,
                    loopbackInterval,
                    startTime,
                    endTime);
            return new ServiceOk<FuturesTrendDirectionReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesTrendDirectionReadModel>(
                GetFuturesTrendDirectionFromRSISignalQuery.ErrorId,
                ex.Message);
        }
    }

    /// <summary>
    /// Gets futures TDI signal.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FuturesTdiSignalReadModel>> GetFuturesTdiSignalAsync(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod = TimeFrameType.OneMinute,
        string configurationId = FuturesTdiConfiguration.StandardConfigurationId)
    {
        try
        {
            FuturesTdiSignalReadModel result =
                (await _dbFactory.MarketDataDb.GetLastFuturesTdiSignalAsync(
                    contractId,
                    valueDate,
                    timePeriod,
                    configurationId))!;
            return new ServiceOk<FuturesTdiSignalReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesTdiSignalReadModel>(GetFuturesTdiSignalQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets futures ITI signal.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <param name="timePeriod">The signal time-frame type.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FuturesItiSignalV2ReadModel>> GetFuturesItiSignalAsync(
        string contractId, DateOnly valueDate, TimeFrameType timePeriod)
    {
        try
        {
            FuturesItiSignalV2ReadModel result =
                (await _dbFactory.MarketDataDb.GetLastFuturesItiSignalAsync(
                    contractId,
                    valueDate,
                    timePeriod))!;
            return new ServiceOk<FuturesItiSignalV2ReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesItiSignalV2ReadModel>(GetFuturesItiSignalQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets futures ITI trend direction changed signals.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <param name="timePeriod">The signal time-frame type.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FuturesItiSignalV2ReadModel[]>> GetFuturesItiTrendDirectionChangedSignalsAsync(
        string contractId, DateOnly valueDate, TimeFrameType timePeriod)
    {
        try
        {
            FuturesItiSignalV2ReadModel[] result =
                [.. await _dbFactory.MarketDataDb.GetFuturesItiTrendDirectionChangedSignalsAsync(
                    contractId,
                    valueDate)];
            return new ServiceOk<FuturesItiSignalV2ReadModel[]>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesItiSignalV2ReadModel[]>(
                GetFuturesItiTrendDirectionChangedSignalsQuery.ErrorId,
                ex.Message);
        }
    }

    /// <summary>
    /// Gets futures ITI signal data.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <param name="timePeriod">The signal time-frame type.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FuturesItiSignalDataReadModel>> GetFuturesItiSignalDataAsync(
        string contractId, DateOnly valueDate, TimeFrameType timePeriod)
    {
        try
        {
            var trendDirectionTask = _dbFactory.MarketDataDb
                .GetLastFuturesItiSignalTrendDirectionChangeAsync(contractId, valueDate);
            var trendExtremeTask = _dbFactory.MarketDataDb
                .GetLastFuturesItiSignalTrendExtremeChangeAsync(contractId, valueDate);
            var trendReversalTask = _dbFactory.MarketDataDb
                .GetLastFuturesItiSignalTrendReversalChangeAsync(contractId, valueDate);
            await Task.WhenAll(trendDirectionTask, trendExtremeTask, trendReversalTask);
            var result = new FuturesItiSignalDataReadModel(
                await trendDirectionTask,
                await trendExtremeTask,
                await trendReversalTask);
            return new ServiceOk<FuturesItiSignalDataReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesItiSignalDataReadModel>(
                GetFuturesItiSignalDataQuery.ErrorId,
                ex.Message);
        }
    }

    /// <summary>
    /// Gets futures ITI MDI distribution.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FuturesItiMDIDistributionReadModel>> GetFuturesItiMDIDistributionAsync(
        string contractId, DateOnly valueDate)
    {
        try
        {
            var result = new FuturesItiMDIDistributionReadModel(
                [.. await _dbFactory.MarketDataDb.GetFuturesItiSignalMDIAsync(contractId, valueDate)]);
            return new ServiceOk<FuturesItiMDIDistributionReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesItiMDIDistributionReadModel>(
                GetFuturesItiMDIDistributionQuery.ErrorId,
                ex.Message);
        }
    }

    /// <summary>
    /// Gets futures ITI MDI distribution by trend.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FuturesItiMDIDistributionReadModel>> GetFuturesItiMDIDistributionByTrendAsync(
        string contractId, DateOnly valueDate)
    {
        try
        {
            var signal = await _dbFactory.MarketDataDb.GetLastFuturesItiSignalAsync(contractId, valueDate);
            var values = await GetFuturesItiSignalMDIByTrendCoreAsync(
                contractId, valueDate, signal?.IntrinsicTimeGroupId ?? 0);
            var result = new FuturesItiMDIDistributionReadModel(values);
            return new ServiceOk<FuturesItiMDIDistributionReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesItiMDIDistributionReadModel>(
                GetFuturesItiMDIDistributionByTrendQuery.ErrorId,
                ex.Message);
        }
    }

    /// <summary>
    /// Gets futures ITI signal MDI.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FuturesItiSignalMDIV2ReadModel[]>> GetFuturesItiSignalMDIAsync(
        string contractId, DateOnly valueDate)
    {
        try
        {
            FuturesItiSignalMDIV2ReadModel[] result =
                [.. await _dbFactory.MarketDataDb.GetFuturesItiSignalMDIAsync(contractId, valueDate)];
            return new ServiceOk<FuturesItiSignalMDIV2ReadModel[]>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesItiSignalMDIV2ReadModel[]>(
                GetFuturesItiSignalMDIQuery.ErrorId,
                ex.Message);
        }
    }

    /// <summary>
    /// Gets futures ITI signal MDI by trend.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <param name="groupId">The intrinsic-time group identifier.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FuturesItiSignalMDIV2ReadModel[]>> GetFuturesItiSignalMDIByTrendAsync(
        string contractId, DateOnly valueDate, int groupId)
    {
        try
        {
            FuturesItiSignalMDIV2ReadModel[] result =
                await GetFuturesItiSignalMDIByTrendCoreAsync(contractId, valueDate, groupId);
            return new ServiceOk<FuturesItiSignalMDIV2ReadModel[]>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesItiSignalMDIV2ReadModel[]>(
                GetFuturesItiSignalMDIByTrendQuery.ErrorId,
                ex.Message);
        }
    }

    /// <summary>
    /// Gets futures ATR signal.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <param name="timePeriod">The signal time-frame type.</param>
    /// <param name="periodLength">The indicator period length.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FuturesAtrSignalReadModel>> GetFuturesAtrSignalAsync(
        string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength)
    {
        try
        {
            FuturesAtrSignalReadModel result =
                (await _dbFactory.MarketDataDb.GetLastFuturesAtrSignalAsync(
                    contractId,
                    valueDate,
                    timePeriod,
                    periodLength))!;
            return new ServiceOk<FuturesAtrSignalReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesAtrSignalReadModel>(GetFuturesAtrSignalQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets futures ATR daily signal.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <param name="timePeriod">The signal time-frame type.</param>
    /// <param name="periodLength">The indicator period length.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FuturesAtrSignalReadModel>> GetFuturesAtrDailySignalAsync(
        string contractId, TimeFrameType timePeriod, int periodLength)
    {
        try
        {
            FuturesAtrSignalReadModel result =
                (await _dbFactory.MarketDataDb.GetLastFuturesAtrDailySignalAsync(
                    contractId,
                    timePeriod,
                    periodLength))!;
            return new ServiceOk<FuturesAtrSignalReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesAtrSignalReadModel>(
                GetFuturesAtrDailySignalQuery.ErrorId,
                ex.Message);
        }
    }

    /// <summary>
    /// Gets futures ADX signal.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <param name="timePeriod">The signal time-frame type.</param>
    /// <param name="periodLength">The indicator period length.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FuturesAdxSignalReadModel>> GetFuturesAdxSignalAsync(
        string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength)
    {
        try
        {
            FuturesAdxSignalReadModel result =
                (await _dbFactory.MarketDataDb.GetLastFuturesAdxSignalAsync(
                    contractId,
                    valueDate,
                    timePeriod,
                    periodLength))!;
            return new ServiceOk<FuturesAdxSignalReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesAdxSignalReadModel>(GetFuturesAdxSignalQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets futures ADX daily signal.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <param name="timePeriod">The signal time-frame type.</param>
    /// <param name="periodLength">The indicator period length.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FuturesAdxSignalReadModel>> GetFuturesAdxDailySignalAsync(
        string contractId, TimeFrameType timePeriod, int periodLength)
    {
        try
        {
            FuturesAdxSignalReadModel result =
                (await _dbFactory.MarketDataDb.GetLastFuturesAdxDailySignalAsync(
                    contractId,
                    timePeriod,
                    periodLength))!;
            return new ServiceOk<FuturesAdxSignalReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesAdxSignalReadModel>(
                GetFuturesAdxDailySignalQuery.ErrorId,
                ex.Message);
        }
    }

    /// <summary>
    /// Gets futures MACD signal.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <param name="timePeriod">The signal time-frame type.</param>
    /// <param name="periodLength">The indicator period length.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FuturesMacdSignalReadModel>> GetFuturesMacdSignalAsync(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        int signalEmaPeriod = FuturesMacdConfiguration.ConventionalSignalEmaPeriod,
        int fastEmaPeriod = FuturesMacdConfiguration.ConventionalFastEmaPeriod,
        int slowEmaPeriod = FuturesMacdConfiguration.ConventionalSlowEmaPeriod)
    {
        try
        {
            FuturesMacdSignalReadModel result =
                (await _dbFactory.MarketDataDb.GetLastFuturesMacdSignalAsync(
                    contractId,
                    valueDate,
                    timePeriod,
                    signalEmaPeriod,
                    fastEmaPeriod,
                    slowEmaPeriod))!;
            return new ServiceOk<FuturesMacdSignalReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesMacdSignalReadModel>(GetFuturesMacdSignalQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets futures MACD daily signal.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <param name="timePeriod">The signal time-frame type.</param>
    /// <param name="periodLength">The indicator period length.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FuturesMacdSignalReadModel>> GetFuturesMacdDailySignalAsync(
        string contractId,
        TimeFrameType timePeriod,
        int periodLength)
    {
        try
        {
            FuturesMacdSignalReadModel result =
                (await _dbFactory.MarketDataDb.GetLastFuturesMacdDailySignalAsync(
                    contractId,
                    timePeriod,
                    periodLength))!;
            return new ServiceOk<FuturesMacdSignalReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesMacdSignalReadModel>(
                GetFuturesMacdDailySignalQuery.ErrorId,
                ex.Message);
        }
    }

    public async Task<ServiceResult<FuturesMacdSignalReadModel>> GetFuturesMacdDailySignalAsync(
        string contractId,
        TimeFrameType timePeriod,
        int signalEmaPeriod,
        int fastEmaPeriod,
        int slowEmaPeriod)
    {
        try
        {
            FuturesMacdSignalReadModel result =
                (await _dbFactory.MarketDataDb.GetLastFuturesMacdDailySignalAsync(
                    contractId,
                    timePeriod,
                    signalEmaPeriod,
                    fastEmaPeriod,
                    slowEmaPeriod))!;
            return new ServiceOk<FuturesMacdSignalReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesMacdSignalReadModel>(
                GetFuturesMacdDailySignalQuery.ErrorId,
                ex.Message);
        }
    }

    async Task<FuturesItiSignalMDIV2ReadModel[]> GetFuturesItiSignalMDIByTrendCoreAsync(
        string contractId, DateOnly valueDate, int groupId)
    {
        var upTrendTask = _dbFactory.MarketDataDb.GetFuturesItiSignalMDIByTrendAsync(
            contractId, valueDate, IntrinsicTimeTrendType.UpTrend, groupId);
        var downTrendTask = _dbFactory.MarketDataDb.GetFuturesItiSignalMDIByTrendAsync(
            contractId, valueDate, IntrinsicTimeTrendType.DownTrend, groupId);
        await Task.WhenAll(upTrendTask, downTrendTask);
        var upTrend = await upTrendTask;
        var downTrend = await downTrendTask;
        return [.. upTrend, .. downTrend];
    }
}
