using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Query.Api;

public sealed partial class ActorMarketDataAnalyticsQueryApi
{
    public Task<ServiceResult<FuturesTradeSignalV2ReadModel>> GetFuturesTradeSignalAsync(
        string contractId,
        DateOnly valueDate,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetFuturesTradeSignalQuery.ErrorId,
            cancellationToken,
            async () => (await _dbFactory.MarketDataDb
                .GetLastFuturesTradeSignalAsync(contractId, valueDate, cancellationToken))!);

    public Task<ServiceResult<FuturesTradeSignalV2ReadModel>> GetLastFuturesTradeSignalAsync(
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetLastFuturesTradeSignalQuery.ErrorId,
            cancellationToken,
            async () => (await _dbFactory.MarketDataDb
                .GetLastFuturesTradeSignalAsync(cancellationToken))!);

    public Task<ServiceResult<FuturesTradeSignalV2ReadModel>> GetFuturesTradeSignalBySymbolAsync(
        string symbol,
        DateOnly valueDate,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetFuturesTradeSignalBySymbolQuery.ErrorId,
            cancellationToken,
            async () => (await _dbFactory.MarketDataDb
                .GetLastFuturesTradeSignalBySymbolAsync(symbol, valueDate, cancellationToken))!);

    public Task<ServiceResult<FuturesTradeSignalId[]>> GetFuturesTradeSignalIdsAsync(
        DateOnly valueDate,
        CancellationToken cancellationToken)
        => ExecuteAsync<FuturesTradeSignalId[]>(
            GetFuturesTradeSignalIdsQuery.ErrorId,
            cancellationToken,
            async () => [.. await _dbFactory.MarketDataDb
                .GetFuturesTradeSignalIdByValueDateAsync(valueDate, cancellationToken)]);

    public Task<ServiceResult<FuturesRsiSignalReadModel>> GetFuturesRsiSignalAsync(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        int periodLength,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetFuturesRsiSignalQuery.ErrorId,
            cancellationToken,
            async () => (await _dbFactory.MarketDataDb.GetLastFuturesRsiSignalAsync(
                contractId, valueDate, timePeriod, periodLength, cancellationToken))!);

    public Task<ServiceResult<FuturesRsiSignalReadModel>> GetFuturesRsiDailySignalAsync(
        string contractId,
        TimeFrameType timePeriod,
        int periodLength,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetFuturesRsiDailySignalQuery.ErrorId,
            cancellationToken,
            async () => (await _dbFactory.MarketDataDb.GetLastFuturesRsiDailySignalAsync(
                contractId, timePeriod, periodLength, cancellationToken))!);

    public Task<ServiceResult<FuturesTrendDirectionReadModel>> GetFuturesTrendDirectionFromRSISignalAsync(
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
            () => _dbFactory.MarketDataDb.GetFuturesTrendDirectionFromRSISignalAsync(
                contractId, valueDate, timePeriod, periodLength,
                timestamp, loopbackInterval, startTime, endTime, cancellationToken));

    public Task<ServiceResult<FuturesTdiSignalReadModel>> GetFuturesTdiSignalAsync(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        string configurationId,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetFuturesTdiSignalQuery.ErrorId,
            cancellationToken,
            async () => (await _dbFactory.MarketDataDb
                .GetLastFuturesTdiSignalAsync(
                    contractId,
                    valueDate,
                    timePeriod,
                    configurationId,
                    cancellationToken))!);

    public Task<ServiceResult<FuturesItiSignalV2ReadModel>> GetFuturesItiSignalAsync(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetFuturesItiSignalQuery.ErrorId,
            cancellationToken,
            async () => (await _dbFactory.MarketDataDb
                .GetLastFuturesItiSignalAsync(contractId, valueDate, timePeriod, cancellationToken))!);

    public Task<ServiceResult<FuturesItiSignalV2ReadModel[]>> GetFuturesItiTrendDirectionChangedSignalsAsync(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        CancellationToken cancellationToken)
        => ExecuteAsync<FuturesItiSignalV2ReadModel[]>(
            GetFuturesItiTrendDirectionChangedSignalsQuery.ErrorId,
            cancellationToken,
            async () => [.. await _dbFactory.MarketDataDb
                .GetFuturesItiTrendDirectionChangedSignalsAsync(contractId, valueDate, cancellationToken)]);

    public Task<ServiceResult<FuturesItiSignalDataReadModel>> GetFuturesItiSignalDataAsync(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetFuturesItiSignalDataQuery.ErrorId,
            cancellationToken,
            async () =>
            {
                var directionTask = _dbFactory.MarketDataDb
                    .GetLastFuturesItiSignalTrendDirectionChangeAsync(contractId, valueDate, cancellationToken);
                var extremeTask = _dbFactory.MarketDataDb
                    .GetLastFuturesItiSignalTrendExtremeChangeAsync(contractId, valueDate, cancellationToken);
                var reversalTask = _dbFactory.MarketDataDb
                    .GetLastFuturesItiSignalTrendReversalChangeAsync(contractId, valueDate, cancellationToken);
                await Task.WhenAll(directionTask, extremeTask, reversalTask).ConfigureAwait(false);
                return new FuturesItiSignalDataReadModel(
                    await directionTask.ConfigureAwait(false),
                    await extremeTask.ConfigureAwait(false),
                    await reversalTask.ConfigureAwait(false));
            });

    public Task<ServiceResult<FuturesItiMDIDistributionReadModel>> GetFuturesItiMDIDistributionAsync(
        string contractId,
        DateOnly valueDate,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetFuturesItiMDIDistributionQuery.ErrorId,
            cancellationToken,
            async () => new FuturesItiMDIDistributionReadModel(
                [.. await _dbFactory.MarketDataDb
                    .GetFuturesItiSignalMDIAsync(contractId, valueDate, cancellationToken)]));

    public Task<ServiceResult<FuturesItiMDIDistributionReadModel>> GetFuturesItiMDIDistributionByTrendAsync(
        string contractId,
        DateOnly valueDate,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetFuturesItiMDIDistributionByTrendQuery.ErrorId,
            cancellationToken,
            async () =>
            {
                var signal = await _dbFactory.MarketDataDb
                    .GetLastFuturesItiSignalAsync(contractId, valueDate, cancellationToken)
                    .ConfigureAwait(false);
                var values = await GetFuturesItiSignalMDIByTrendCoreAsync(
                    contractId,
                    valueDate,
                    signal?.IntrinsicTimeGroupId ?? 0,
                    cancellationToken).ConfigureAwait(false);
                return new FuturesItiMDIDistributionReadModel(values);
            });

    public Task<ServiceResult<FuturesItiSignalMDIV2ReadModel[]>> GetFuturesItiSignalMDIAsync(
        string contractId,
        DateOnly valueDate,
        CancellationToken cancellationToken)
        => ExecuteAsync<FuturesItiSignalMDIV2ReadModel[]>(
            GetFuturesItiSignalMDIQuery.ErrorId,
            cancellationToken,
            async () => [.. await _dbFactory.MarketDataDb
                .GetFuturesItiSignalMDIAsync(contractId, valueDate, cancellationToken)]);

    public Task<ServiceResult<FuturesItiSignalMDIV2ReadModel[]>> GetFuturesItiSignalMDIByTrendAsync(
        string contractId,
        DateOnly valueDate,
        int groupId,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetFuturesItiSignalMDIByTrendQuery.ErrorId,
            cancellationToken,
            () => GetFuturesItiSignalMDIByTrendCoreAsync(
                contractId, valueDate, groupId, cancellationToken));

    public Task<ServiceResult<FuturesAtrSignalReadModel>> GetFuturesAtrSignalAsync(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        int periodLength,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetFuturesAtrSignalQuery.ErrorId,
            cancellationToken,
            async () => (await _dbFactory.MarketDataDb.GetLastFuturesAtrSignalAsync(
                contractId, valueDate, timePeriod, periodLength, cancellationToken))!);

    public Task<ServiceResult<FuturesAtrSignalReadModel>> GetFuturesAtrDailySignalAsync(
        string contractId,
        TimeFrameType timePeriod,
        int periodLength,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetFuturesAtrDailySignalQuery.ErrorId,
            cancellationToken,
            async () => (await _dbFactory.MarketDataDb.GetLastFuturesAtrDailySignalAsync(
                contractId, timePeriod, periodLength, cancellationToken))!);

    public Task<ServiceResult<FuturesAdxSignalReadModel>> GetFuturesAdxSignalAsync(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        int periodLength,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetFuturesAdxSignalQuery.ErrorId,
            cancellationToken,
            async () => (await _dbFactory.MarketDataDb.GetLastFuturesAdxSignalAsync(
                contractId, valueDate, timePeriod, periodLength, cancellationToken))!);

    public Task<ServiceResult<FuturesAdxSignalReadModel>> GetFuturesAdxDailySignalAsync(
        string contractId,
        TimeFrameType timePeriod,
        int periodLength,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetFuturesAdxDailySignalQuery.ErrorId,
            cancellationToken,
            async () => (await _dbFactory.MarketDataDb.GetLastFuturesAdxDailySignalAsync(
                contractId, timePeriod, periodLength, cancellationToken))!);

    public Task<ServiceResult<FuturesMacdSignalReadModel>> GetFuturesMacdSignalAsync(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        int periodLength,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetFuturesMacdSignalQuery.ErrorId,
            cancellationToken,
            async () => (await _dbFactory.MarketDataDb.GetLastFuturesMacdSignalAsync(
                contractId, valueDate, timePeriod, periodLength, cancellationToken))!);

    public Task<ServiceResult<FuturesMacdSignalReadModel>> GetFuturesMacdDailySignalAsync(
        string contractId,
        TimeFrameType timePeriod,
        int periodLength,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetFuturesMacdDailySignalQuery.ErrorId,
            cancellationToken,
            async () => (await _dbFactory.MarketDataDb.GetLastFuturesMacdDailySignalAsync(
                contractId, timePeriod, periodLength, cancellationToken))!);

    async Task<FuturesItiSignalMDIV2ReadModel[]> GetFuturesItiSignalMDIByTrendCoreAsync(
        string contractId,
        DateOnly valueDate,
        int groupId,
        CancellationToken cancellationToken)
    {
        var upTrendTask = _dbFactory.MarketDataDb.GetFuturesItiSignalMDIByTrendAsync(
            contractId, valueDate, IntrinsicTimeTrendType.UpTrend, groupId, cancellationToken);
        var downTrendTask = _dbFactory.MarketDataDb.GetFuturesItiSignalMDIByTrendAsync(
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
