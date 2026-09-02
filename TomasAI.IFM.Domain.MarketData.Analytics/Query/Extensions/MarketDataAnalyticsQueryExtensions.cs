using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Query.Actor;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Application.MarketData.MarketOutlook;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Query.Extensions;

/// <summary>
/// Provides direct, in-process Market Data Analytics queries without actor messaging.
/// </summary>
/// <remarks>
/// Durable signal and distribution data is read through <see cref="IDbContextFactory.MarketDataDb"/>;
/// the derived Market Outlook is read from its process-local hot cache. Every public operation owns its
/// exception handling and returns a typed service result using the corresponding query error identifier.
/// </remarks>
public static partial class MarketDataAnalyticsQueryExtensions
{

    /// <summary>Executes the GetMarketOutlookSnapshotAsync operation.</summary>
    public static async Task<ServiceResult<MarketOutlookReadModel>> GetMarketOutlookSnapshotAsync(this IFuturesTradeSignalQueryContext context,
        string contractId,
        DateOnly valueDate,
        bool loadPersistedBaseline = false)
    {
        try
        {
            var entityId = new MarketOutlookEntityId(contractId, valueDate);
            var result = loadPersistedBaseline && context.MarketOutlookHydrator is { } hydrator
                ? await hydrator.HydrateAsync(entityId).ConfigureAwait(false)
                : MarketOutlookHotCache.Shared.TryGetCurrent(entityId, out var cached)
                    ? cached
                    : null;
            result ??= new MarketOutlookReadModel
            {
                ContractId = contractId,
                ValueDate = valueDate,
                UpdatedAtUtc = DateTime.UtcNow,
                MissingInputs = "Market Outlook unavailable",
                FeedHealth = "Unavailable"
            };
            return new ServiceOk<MarketOutlookReadModel>(result!);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<MarketOutlookReadModel>(
                GetMarketOutlookSnapshotQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets futures trade signal.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public static async Task<ServiceResult<FuturesTradeSignalV2ReadModel>> GetFuturesTradeSignalAsync(this IFuturesTradeSignalQueryContext context,
        string contractId, DateOnly valueDate)
    {
        try
        {
            FuturesTradeSignalV2ReadModel result =
                (await context.DbFactory.MarketDataDb.GetLastFuturesTradeSignalAsync(contractId, valueDate))!;
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
    public static async Task<ServiceResult<FuturesTradeSignalV2ReadModel>> GetLastFuturesTradeSignalAsync(this IFuturesTradeSignalQueryContext context)
    {
        try
        {
            FuturesTradeSignalV2ReadModel result =
                (await context.DbFactory.MarketDataDb.GetLastFuturesTradeSignalAsync())!;
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
    public static async Task<ServiceResult<FuturesTradeSignalV2ReadModel>> GetFuturesTradeSignalBySymbolAsync(this IFuturesTradeSignalQueryContext context,
        string symbol, DateOnly valueDate)
    {
        try
        {
            FuturesTradeSignalV2ReadModel result =
                (await context.DbFactory.MarketDataDb.GetLastFuturesTradeSignalBySymbolAsync(symbol, valueDate))!;
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
    public static async Task<ServiceResult<FuturesTradeSignalId[]>> GetFuturesTradeSignalIdsAsync(this IFuturesTradeSignalQueryContext context, DateOnly valueDate)
    {
        try
        {
            FuturesTradeSignalId[] result =
                [.. await context.DbFactory.MarketDataDb.GetFuturesTradeSignalIdByValueDateAsync(valueDate)];
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
    public static async Task<ServiceResult<FuturesRsiSignalReadModel>> GetFuturesRsiSignalAsync(this IFuturesTradeSignalQueryContext context,
        string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength)
    {
        try
        {
            FuturesRsiSignalReadModel result =
                (await context.DbFactory.MarketDataDb.GetLastFuturesRsiSignalAsync(
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
    public static async Task<ServiceResult<FuturesRsiSignalReadModel>> GetFuturesRsiDailySignalAsync(this IFuturesTradeSignalQueryContext context,
        string contractId, TimeFrameType timePeriod, int periodLength)
    {
        try
        {
            FuturesRsiSignalReadModel result =
                (await context.DbFactory.MarketDataDb.GetLastFuturesRsiDailySignalAsync(
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
    public static async Task<ServiceResult<FuturesTrendDirectionReadModel>> GetFuturesTrendDirectionFromRSISignalAsync(this IFuturesTradeSignalQueryContext context,
        string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength,
        DateTime timestamp, int loopbackInterval,
        DateTime startTime, DateTime endTime)
    {
        try
        {
            FuturesTrendDirectionReadModel result =
                await context.DbFactory.MarketDataDb.GetFuturesTrendDirectionFromRSISignalAsync(
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
    public static async Task<ServiceResult<FuturesTdiSignalReadModel>> GetFuturesTdiSignalAsync(this IFuturesTradeSignalQueryContext context,
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod = TimeFrameType.OneMinute,
        string configurationId = FuturesTdiConfiguration.StandardConfigurationId)
    {
        try
        {
            FuturesTdiSignalReadModel result =
                (await context.DbFactory.MarketDataDb.GetLastFuturesTdiSignalAsync(
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
    public static async Task<ServiceResult<FuturesItiSignalV2ReadModel>> GetFuturesItiSignalAsync(this IFuturesTradeSignalQueryContext context,
        string contractId, DateOnly valueDate, TimeFrameType timePeriod)
    {
        try
        {
            FuturesItiSignalV2ReadModel result =
                (await context.DbFactory.MarketDataDb.GetLastFuturesItiSignalAsync(
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
    public static async Task<ServiceResult<FuturesItiSignalV2ReadModel[]>> GetFuturesItiTrendDirectionChangedSignalsAsync(this IFuturesTradeSignalQueryContext context,
        string contractId, DateOnly valueDate, TimeFrameType timePeriod)
    {
        try
        {
            FuturesItiSignalV2ReadModel[] result =
                [.. await context.DbFactory.MarketDataDb.GetFuturesItiTrendDirectionChangedSignalsAsync(
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
    public static async Task<ServiceResult<FuturesItiSignalDataReadModel>> GetFuturesItiSignalDataAsync(this IFuturesTradeSignalQueryContext context,
        string contractId, DateOnly valueDate, TimeFrameType timePeriod)
    {
        try
        {
            var trendDirectionTask = context.DbFactory.MarketDataDb
                .GetLastFuturesItiSignalTrendDirectionChangeAsync(contractId, valueDate);
            var trendExtremeTask = context.DbFactory.MarketDataDb
                .GetLastFuturesItiSignalTrendExtremeChangeAsync(contractId, valueDate);
            var trendReversalTask = context.DbFactory.MarketDataDb
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
    public static async Task<ServiceResult<FuturesItiMDIDistributionReadModel>> GetFuturesItiMDIDistributionAsync(this IFuturesTradeSignalQueryContext context,
        string contractId, DateOnly valueDate)
    {
        try
        {
            var result = new FuturesItiMDIDistributionReadModel(
                [.. await context.DbFactory.MarketDataDb.GetFuturesItiSignalMDIAsync(contractId, valueDate)]);
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
    public static async Task<ServiceResult<FuturesItiMDIDistributionReadModel>> GetFuturesItiMDIDistributionByTrendAsync(this IFuturesTradeSignalQueryContext context,
        string contractId, DateOnly valueDate)
    {
        try
        {
            var signal = await context.DbFactory.MarketDataDb.GetLastFuturesItiSignalAsync(contractId, valueDate);
            var values = await GetFuturesItiSignalMDIByTrendCoreAsync(context,
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
    public static async Task<ServiceResult<FuturesItiSignalMDIV2ReadModel[]>> GetFuturesItiSignalMDIAsync(this IFuturesTradeSignalQueryContext context,
        string contractId, DateOnly valueDate)
    {
        try
        {
            FuturesItiSignalMDIV2ReadModel[] result =
                [.. await context.DbFactory.MarketDataDb.GetFuturesItiSignalMDIAsync(contractId, valueDate)];
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
    public static async Task<ServiceResult<FuturesItiSignalMDIV2ReadModel[]>> GetFuturesItiSignalMDIByTrendAsync(this IFuturesTradeSignalQueryContext context,
        string contractId, DateOnly valueDate, int groupId)
    {
        try
        {
            FuturesItiSignalMDIV2ReadModel[] result =
                await GetFuturesItiSignalMDIByTrendCoreAsync(context, contractId, valueDate, groupId);
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
    public static async Task<ServiceResult<FuturesAtrSignalReadModel>> GetFuturesAtrSignalAsync(this IFuturesTradeSignalQueryContext context,
        string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength)
    {
        try
        {
            FuturesAtrSignalReadModel result =
                (await context.DbFactory.MarketDataDb.GetLastFuturesAtrSignalAsync(
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
    public static async Task<ServiceResult<FuturesAtrSignalReadModel>> GetFuturesAtrDailySignalAsync(this IFuturesTradeSignalQueryContext context,
        string contractId, TimeFrameType timePeriod, int periodLength)
    {
        try
        {
            FuturesAtrSignalReadModel result =
                (await context.DbFactory.MarketDataDb.GetLastFuturesAtrDailySignalAsync(
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
    public static async Task<ServiceResult<FuturesAdxSignalReadModel>> GetFuturesAdxSignalAsync(this IFuturesTradeSignalQueryContext context,
        string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength)
    {
        try
        {
            FuturesAdxSignalReadModel result =
                (await context.DbFactory.MarketDataDb.GetLastFuturesAdxSignalAsync(
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
    public static async Task<ServiceResult<FuturesAdxSignalReadModel>> GetFuturesAdxDailySignalAsync(this IFuturesTradeSignalQueryContext context,
        string contractId, TimeFrameType timePeriod, int periodLength)
    {
        try
        {
            FuturesAdxSignalReadModel result =
                (await context.DbFactory.MarketDataDb.GetLastFuturesAdxDailySignalAsync(
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
    public static async Task<ServiceResult<FuturesMacdSignalReadModel>> GetFuturesMacdSignalAsync(this IFuturesTradeSignalQueryContext context,
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
                (await context.DbFactory.MarketDataDb.GetLastFuturesMacdSignalAsync(
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
    public static async Task<ServiceResult<FuturesMacdSignalReadModel>> GetFuturesMacdDailySignalAsync(this IFuturesTradeSignalQueryContext context,
        string contractId,
        TimeFrameType timePeriod,
        int periodLength)
    {
        try
        {
            FuturesMacdSignalReadModel result =
                (await context.DbFactory.MarketDataDb.GetLastFuturesMacdDailySignalAsync(
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

    /// <summary>Executes the GetFuturesMacdDailySignalAsync operation.</summary>
    public static async Task<ServiceResult<FuturesMacdSignalReadModel>> GetFuturesMacdDailySignalAsync(this IFuturesTradeSignalQueryContext context,
        string contractId,
        TimeFrameType timePeriod,
        int signalEmaPeriod,
        int fastEmaPeriod,
        int slowEmaPeriod)
    {
        try
        {
            FuturesMacdSignalReadModel result =
                (await context.DbFactory.MarketDataDb.GetLastFuturesMacdDailySignalAsync(
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

    static async Task<FuturesItiSignalMDIV2ReadModel[]> GetFuturesItiSignalMDIByTrendCoreAsync(
        IFuturesTradeSignalQueryContext context,
        string contractId, DateOnly valueDate, int groupId)
    {
        var upTrendTask = context.DbFactory.MarketDataDb.GetFuturesItiSignalMDIByTrendAsync(
            contractId, valueDate, IntrinsicTimeTrendType.UpTrend, groupId);
        var downTrendTask = context.DbFactory.MarketDataDb.GetFuturesItiSignalMDIByTrendAsync(
            contractId, valueDate, IntrinsicTimeTrendType.DownTrend, groupId);
        await Task.WhenAll(upTrendTask, downTrendTask);
        var upTrend = await upTrendTask;
        var downTrend = await downTrendTask;
        return [.. upTrend, .. downTrend];
    }
}
