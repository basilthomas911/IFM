using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Application.MarketData.MarketOutlook;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Query.Extensions;

/// <summary>Provides the MarketDataAnalyticsQueryExtensions implementation.</summary>
public static partial class MarketDataAnalyticsQueryExtensions
{
    /// <summary>Executes the GetMarketOutlookSnapshotAsync operation.</summary>
    public static Task<ServiceResult<MarketOutlookReadModel>> GetMarketOutlookSnapshotAsync(this IFuturesTradeSignalQueryContext context,
        string contractId,
        DateOnly valueDate,
        CancellationToken cancellationToken,
        bool loadPersistedBaseline = false)
        => ExecuteAsync(
            GetMarketOutlookSnapshotQuery.ErrorId,
            cancellationToken,
            async () =>
            {
                var entityId = new MarketOutlookEntityId(contractId, valueDate);
                var result = loadPersistedBaseline && context.MarketOutlookHydrator is { } hydrator
                    ? await hydrator.HydrateAsync(entityId, cancellationToken).ConfigureAwait(false)
                    : MarketOutlookHotCache.Shared.TryGetCurrent(entityId, out var cached)
                        ? cached
                        : null;
                return result ?? new MarketOutlookReadModel
                {
                    ContractId = contractId,
                    ValueDate = valueDate,
                    UpdatedAtUtc = DateTime.UtcNow,
                    MissingInputs = "Market Outlook unavailable",
                    FeedHealth = "Unavailable"
                };
            });

    /// <summary>Executes the GetFuturesTradeSignalAsync operation.</summary>
    public static Task<ServiceResult<FuturesTradeSignalV2ReadModel>> GetFuturesTradeSignalAsync(this IFuturesTradeSignalQueryContext context,
        string contractId,
        DateOnly valueDate,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetFuturesTradeSignalQuery.ErrorId,
            cancellationToken,
            async () => (await context.DbFactory.MarketDataDb
                .GetLastFuturesTradeSignalAsync(contractId, valueDate, cancellationToken))!);

    /// <summary>Executes the GetLastFuturesTradeSignalAsync operation.</summary>
    public static Task<ServiceResult<FuturesTradeSignalV2ReadModel>> GetLastFuturesTradeSignalAsync(this IFuturesTradeSignalQueryContext context,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetLastFuturesTradeSignalQuery.ErrorId,
            cancellationToken,
            async () => (await context.DbFactory.MarketDataDb
                .GetLastFuturesTradeSignalAsync(cancellationToken))!);

    /// <summary>Executes the GetFuturesTradeSignalBySymbolAsync operation.</summary>
    public static Task<ServiceResult<FuturesTradeSignalV2ReadModel>> GetFuturesTradeSignalBySymbolAsync(this IFuturesTradeSignalQueryContext context,
        string symbol,
        DateOnly valueDate,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetFuturesTradeSignalBySymbolQuery.ErrorId,
            cancellationToken,
            async () => (await context.DbFactory.MarketDataDb
                .GetLastFuturesTradeSignalBySymbolAsync(symbol, valueDate, cancellationToken))!);

    /// <summary>Executes the GetFuturesTradeSignalIdsAsync operation.</summary>
    public static Task<ServiceResult<FuturesTradeSignalId[]>> GetFuturesTradeSignalIdsAsync(this IFuturesTradeSignalQueryContext context,
        DateOnly valueDate,
        CancellationToken cancellationToken)
        => ExecuteAsync<FuturesTradeSignalId[]>(
            GetFuturesTradeSignalIdsQuery.ErrorId,
            cancellationToken,
            async () => [.. await context.DbFactory.MarketDataDb
                .GetFuturesTradeSignalIdByValueDateAsync(valueDate, cancellationToken)]);

    /// <summary>Executes the GetFuturesRsiSignalAsync operation.</summary>
    public static Task<ServiceResult<FuturesRsiSignalReadModel>> GetFuturesRsiSignalAsync(this IFuturesTradeSignalQueryContext context,
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        int periodLength,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetFuturesRsiSignalQuery.ErrorId,
            cancellationToken,
            async () => (await context.DbFactory.MarketDataDb.GetLastFuturesRsiSignalAsync(
                contractId, valueDate, timePeriod, periodLength, cancellationToken))!);

    /// <summary>Executes the GetFuturesRsiDailySignalAsync operation.</summary>
    public static Task<ServiceResult<FuturesRsiSignalReadModel>> GetFuturesRsiDailySignalAsync(this IFuturesTradeSignalQueryContext context,
        string contractId,
        TimeFrameType timePeriod,
        int periodLength,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetFuturesRsiDailySignalQuery.ErrorId,
            cancellationToken,
            async () => (await context.DbFactory.MarketDataDb.GetLastFuturesRsiDailySignalAsync(
                contractId, timePeriod, periodLength, cancellationToken))!);

    /// <summary>Executes the GetFuturesTrendDirectionFromRSISignalAsync operation.</summary>
    public static Task<ServiceResult<FuturesTrendDirectionReadModel>> GetFuturesTrendDirectionFromRSISignalAsync(this IFuturesTradeSignalQueryContext context,
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        int periodLength,
        DateTime timestamp,
        int loopbackInterval,
        DateTime startTime,
        DateTime endTime,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetFuturesTrendDirectionFromRSISignalQuery.ErrorId,
            cancellationToken,
            () => context.DbFactory.MarketDataDb.GetFuturesTrendDirectionFromRSISignalAsync(
                contractId, valueDate, timePeriod, periodLength,
                timestamp, loopbackInterval, startTime, endTime, cancellationToken));

    /// <summary>Executes the GetFuturesTdiSignalAsync operation.</summary>
    public static Task<ServiceResult<FuturesTdiSignalReadModel>> GetFuturesTdiSignalAsync(this IFuturesTradeSignalQueryContext context,
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        string configurationId,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetFuturesTdiSignalQuery.ErrorId,
            cancellationToken,
            async () => (await context.DbFactory.MarketDataDb
                .GetLastFuturesTdiSignalAsync(
                    contractId,
                    valueDate,
                    timePeriod,
                    configurationId,
                    cancellationToken))!);

    /// <summary>Executes the GetFuturesItiSignalAsync operation.</summary>
    public static Task<ServiceResult<FuturesItiSignalV2ReadModel>> GetFuturesItiSignalAsync(this IFuturesTradeSignalQueryContext context,
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetFuturesItiSignalQuery.ErrorId,
            cancellationToken,
            async () => (await context.DbFactory.MarketDataDb
                .GetLastFuturesItiSignalAsync(contractId, valueDate, timePeriod, cancellationToken))!);

    /// <summary>Executes the GetFuturesItiTrendDirectionChangedSignalsAsync operation.</summary>
    public static Task<ServiceResult<FuturesItiSignalV2ReadModel[]>> GetFuturesItiTrendDirectionChangedSignalsAsync(this IFuturesTradeSignalQueryContext context,
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        CancellationToken cancellationToken)
        => ExecuteAsync<FuturesItiSignalV2ReadModel[]>(
            GetFuturesItiTrendDirectionChangedSignalsQuery.ErrorId,
            cancellationToken,
            async () => [.. await context.DbFactory.MarketDataDb
                .GetFuturesItiTrendDirectionChangedSignalsAsync(contractId, valueDate, cancellationToken)]);

    /// <summary>Executes the GetFuturesItiSignalDataAsync operation.</summary>
    public static Task<ServiceResult<FuturesItiSignalDataReadModel>> GetFuturesItiSignalDataAsync(this IFuturesTradeSignalQueryContext context,
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetFuturesItiSignalDataQuery.ErrorId,
            cancellationToken,
            async () =>
            {
                var directionTask = context.DbFactory.MarketDataDb
                    .GetLastFuturesItiSignalTrendDirectionChangeAsync(contractId, valueDate, cancellationToken);
                var extremeTask = context.DbFactory.MarketDataDb
                    .GetLastFuturesItiSignalTrendExtremeChangeAsync(contractId, valueDate, cancellationToken);
                var reversalTask = context.DbFactory.MarketDataDb
                    .GetLastFuturesItiSignalTrendReversalChangeAsync(contractId, valueDate, cancellationToken);
                await Task.WhenAll(directionTask, extremeTask, reversalTask).ConfigureAwait(false);
                return new FuturesItiSignalDataReadModel(
                    await directionTask.ConfigureAwait(false),
                    await extremeTask.ConfigureAwait(false),
                    await reversalTask.ConfigureAwait(false));
            });

    /// <summary>Executes the GetFuturesItiMDIDistributionAsync operation.</summary>
    public static Task<ServiceResult<FuturesItiMDIDistributionReadModel>> GetFuturesItiMDIDistributionAsync(this IFuturesTradeSignalQueryContext context,
        string contractId,
        DateOnly valueDate,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetFuturesItiMDIDistributionQuery.ErrorId,
            cancellationToken,
            async () => new FuturesItiMDIDistributionReadModel(
                [.. await context.DbFactory.MarketDataDb
                    .GetFuturesItiSignalMDIAsync(contractId, valueDate, cancellationToken)]));

    /// <summary>Executes the GetFuturesItiMDIDistributionByTrendAsync operation.</summary>
    public static Task<ServiceResult<FuturesItiMDIDistributionReadModel>> GetFuturesItiMDIDistributionByTrendAsync(this IFuturesTradeSignalQueryContext context,
        string contractId,
        DateOnly valueDate,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetFuturesItiMDIDistributionByTrendQuery.ErrorId,
            cancellationToken,
            async () =>
            {
                var signal = await context.DbFactory.MarketDataDb
                    .GetLastFuturesItiSignalAsync(contractId, valueDate, cancellationToken)
                    .ConfigureAwait(false);
                var values = await GetFuturesItiSignalMDIByTrendCoreAsync(context,
                    contractId,
                    valueDate,
                    signal?.IntrinsicTimeGroupId ?? 0,
                    cancellationToken).ConfigureAwait(false);
                return new FuturesItiMDIDistributionReadModel(values);
            });

    /// <summary>Executes the GetFuturesItiSignalMDIAsync operation.</summary>
    public static Task<ServiceResult<FuturesItiSignalMDIV2ReadModel[]>> GetFuturesItiSignalMDIAsync(this IFuturesTradeSignalQueryContext context,
        string contractId,
        DateOnly valueDate,
        CancellationToken cancellationToken)
        => ExecuteAsync<FuturesItiSignalMDIV2ReadModel[]>(
            GetFuturesItiSignalMDIQuery.ErrorId,
            cancellationToken,
            async () => [.. await context.DbFactory.MarketDataDb
                .GetFuturesItiSignalMDIAsync(contractId, valueDate, cancellationToken)]);

    /// <summary>Executes the GetFuturesItiSignalMDIByTrendAsync operation.</summary>
    public static Task<ServiceResult<FuturesItiSignalMDIV2ReadModel[]>> GetFuturesItiSignalMDIByTrendAsync(this IFuturesTradeSignalQueryContext context,
        string contractId,
        DateOnly valueDate,
        int groupId,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetFuturesItiSignalMDIByTrendQuery.ErrorId,
            cancellationToken,
            () => GetFuturesItiSignalMDIByTrendCoreAsync(context,
                contractId, valueDate, groupId, cancellationToken));

    /// <summary>Executes the GetFuturesAtrSignalAsync operation.</summary>
    public static Task<ServiceResult<FuturesAtrSignalReadModel>> GetFuturesAtrSignalAsync(this IFuturesTradeSignalQueryContext context,
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        int periodLength,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetFuturesAtrSignalQuery.ErrorId,
            cancellationToken,
            async () => (await context.DbFactory.MarketDataDb.GetLastFuturesAtrSignalAsync(
                contractId, valueDate, timePeriod, periodLength, cancellationToken))!);

    /// <summary>Executes the GetFuturesAtrDailySignalAsync operation.</summary>
    public static Task<ServiceResult<FuturesAtrSignalReadModel>> GetFuturesAtrDailySignalAsync(this IFuturesTradeSignalQueryContext context,
        string contractId,
        TimeFrameType timePeriod,
        int periodLength,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetFuturesAtrDailySignalQuery.ErrorId,
            cancellationToken,
            async () => (await context.DbFactory.MarketDataDb.GetLastFuturesAtrDailySignalAsync(
                contractId, timePeriod, periodLength, cancellationToken))!);

    /// <summary>Executes the GetFuturesAdxSignalAsync operation.</summary>
    public static Task<ServiceResult<FuturesAdxSignalReadModel>> GetFuturesAdxSignalAsync(this IFuturesTradeSignalQueryContext context,
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        int periodLength,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetFuturesAdxSignalQuery.ErrorId,
            cancellationToken,
            async () => (await context.DbFactory.MarketDataDb.GetLastFuturesAdxSignalAsync(
                contractId, valueDate, timePeriod, periodLength, cancellationToken))!);

    /// <summary>Executes the GetFuturesAdxDailySignalAsync operation.</summary>
    public static Task<ServiceResult<FuturesAdxSignalReadModel>> GetFuturesAdxDailySignalAsync(this IFuturesTradeSignalQueryContext context,
        string contractId,
        TimeFrameType timePeriod,
        int periodLength,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetFuturesAdxDailySignalQuery.ErrorId,
            cancellationToken,
            async () => (await context.DbFactory.MarketDataDb.GetLastFuturesAdxDailySignalAsync(
                contractId, timePeriod, periodLength, cancellationToken))!);

    /// <summary>Executes the GetFuturesMacdSignalAsync operation.</summary>
    public static Task<ServiceResult<FuturesMacdSignalReadModel>> GetFuturesMacdSignalAsync(this IFuturesTradeSignalQueryContext context,
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        int periodLength,
        CancellationToken cancellationToken)
        => GetFuturesMacdSignalAsync(
            context,
            contractId,
            valueDate,
            timePeriod,
            periodLength,
            FuturesMacdConfiguration.ConventionalFastEmaPeriod,
            FuturesMacdConfiguration.ConventionalSlowEmaPeriod,
            cancellationToken);

    /// <summary>Executes the GetFuturesMacdSignalAsync operation.</summary>
    public static Task<ServiceResult<FuturesMacdSignalReadModel>> GetFuturesMacdSignalAsync(this IFuturesTradeSignalQueryContext context,
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        int signalEmaPeriod,
        int fastEmaPeriod,
        int slowEmaPeriod,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetFuturesMacdSignalQuery.ErrorId,
            cancellationToken,
            async () => (await context.DbFactory.MarketDataDb.GetLastFuturesMacdSignalAsync(
                contractId,
                valueDate,
                timePeriod,
                signalEmaPeriod,
                fastEmaPeriod,
                slowEmaPeriod,
                cancellationToken))!);

    /// <summary>Executes the GetFuturesMacdDailySignalAsync operation.</summary>
    public static Task<ServiceResult<FuturesMacdSignalReadModel>> GetFuturesMacdDailySignalAsync(this IFuturesTradeSignalQueryContext context,
        string contractId,
        TimeFrameType timePeriod,
        int periodLength,
        CancellationToken cancellationToken)
        => GetFuturesMacdDailySignalAsync(
            context,
            contractId,
            timePeriod,
            periodLength,
            FuturesMacdConfiguration.ConventionalFastEmaPeriod,
            FuturesMacdConfiguration.ConventionalSlowEmaPeriod,
            cancellationToken);

    /// <summary>Executes the GetFuturesMacdDailySignalAsync operation.</summary>
    public static Task<ServiceResult<FuturesMacdSignalReadModel>> GetFuturesMacdDailySignalAsync(this IFuturesTradeSignalQueryContext context,
        string contractId,
        TimeFrameType timePeriod,
        int signalEmaPeriod,
        int fastEmaPeriod,
        int slowEmaPeriod,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetFuturesMacdDailySignalQuery.ErrorId,
            cancellationToken,
            async () => (await context.DbFactory.MarketDataDb.GetLastFuturesMacdDailySignalAsync(
                contractId,
                timePeriod,
                signalEmaPeriod,
                fastEmaPeriod,
                slowEmaPeriod,
                cancellationToken))!);

    static async Task<FuturesItiSignalMDIV2ReadModel[]> GetFuturesItiSignalMDIByTrendCoreAsync(
        IFuturesTradeSignalQueryContext context,
        string contractId,
        DateOnly valueDate,
        int groupId,
        CancellationToken cancellationToken)
    {
        var upTrendTask = context.DbFactory.MarketDataDb.GetFuturesItiSignalMDIByTrendAsync(
            contractId, valueDate, IntrinsicTimeTrendType.UpTrend, groupId, cancellationToken);
        var downTrendTask = context.DbFactory.MarketDataDb.GetFuturesItiSignalMDIByTrendAsync(
            contractId, valueDate, IntrinsicTimeTrendType.DownTrend, groupId, cancellationToken);
        await Task.WhenAll(upTrendTask, downTrendTask).ConfigureAwait(false);
        var upTrend = await upTrendTask.ConfigureAwait(false);
        var downTrend = await downTrendTask.ConfigureAwait(false);
        return [.. upTrend, .. downTrend];
    }

    static async Task<ServiceResult<T>> ExecuteAsync<T>(
        int errorId,
        CancellationToken cancellationToken,
        Func<Task<T>> operation)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await operation().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return new ServiceOk<T>(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ServiceFailed<T>(errorId, ex.Message);
        }
    }
}
