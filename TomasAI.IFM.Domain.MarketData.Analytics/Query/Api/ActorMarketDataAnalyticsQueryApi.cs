using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Query.Api;

/// <summary>Provides direct, in-process Market Data Analytics queries without actor messaging.</summary>
public sealed class ActorMarketDataAnalyticsQueryApi(IDbContextFactory dbFactory)
    : IActorMarketDataAnalyticsQueryApi
{
    readonly IDbContextFactory _dbFactory = IsArgumentNull.Set(dbFactory);

    public Task<ServiceResult<FuturesTradeSignalV2ReadModel>> GetFuturesTradeSignalAsync(
        string contractId, DateOnly valueDate)
        => ExecuteAsync(GetFuturesTradeSignalQuery.ErrorId,
            async () => (await _dbFactory.MarketDataDb.GetLastFuturesTradeSignalAsync(contractId, valueDate))!);

    public Task<ServiceResult<FuturesTradeSignalV2ReadModel>> GetLastFuturesTradeSignalAsync()
        => ExecuteAsync(GetLastFuturesTradeSignalQuery.ErrorId,
            async () => (await _dbFactory.MarketDataDb.GetLastFuturesTradeSignalAsync())!);

    public Task<ServiceResult<FuturesTradeSignalV2ReadModel>> GetFuturesTradeSignalBySymbolAsync(
        string symbol, DateOnly valueDate)
        => ExecuteAsync(GetFuturesTradeSignalBySymbolQuery.ErrorId,
            async () => (await _dbFactory.MarketDataDb.GetLastFuturesTradeSignalBySymbolAsync(symbol, valueDate))!);

    public Task<ServiceResult<FuturesTradeSignalId[]>> GetFuturesTradeSignalIdsAsync(DateOnly valueDate)
        => ExecuteAsync<FuturesTradeSignalId[]>(GetFuturesTradeSignalIdsQuery.ErrorId,
            async () => [.. await _dbFactory.MarketDataDb.GetFuturesTradeSignalIdByValueDateAsync(valueDate)]);

    public Task<ServiceResult<FuturesRsiSignalReadModel>> GetFuturesRsiSignalAsync(
        string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength)
        => ExecuteAsync(GetFuturesRsiSignalQuery.ErrorId,
            async () => (await _dbFactory.MarketDataDb.GetLastFuturesRsiSignalAsync(
                contractId, valueDate, timePeriod, periodLength))!);

    public Task<ServiceResult<FuturesTrendDirectionReadModel>> GetFuturesTrendDirectionFromRSISignalAsync(
        string contractId, DateOnly valueDate, DateTime timestamp, int loopbackInterval,
        DateTime startTime, DateTime endTime)
        => ExecuteAsync(GetFuturesTrendDirectionFromRSISignalQuery.ErrorId,
            async () => await _dbFactory.MarketDataDb.GetFuturesTrendDirectionFromRSISignalAsync(
                contractId, valueDate, timestamp, loopbackInterval, startTime, endTime));

    public Task<ServiceResult<FuturesTdiSignalReadModel>> GetFuturesTdiSignalAsync(
        string contractId, DateOnly valueDate)
        => ExecuteAsync(GetFuturesTdiSignalQuery.ErrorId,
            async () => (await _dbFactory.MarketDataDb.GetLastFuturesTdiSignalAsync(contractId, valueDate))!);

    public Task<ServiceResult<FuturesItiSignalV2ReadModel>> GetFuturesItiSignalAsync(
        string contractId, DateOnly valueDate, TimeFrameType timePeriod)
        => ExecuteAsync(GetFuturesItiSignalQuery.ErrorId,
            async () => (await _dbFactory.MarketDataDb.GetLastFuturesItiSignalAsync(contractId, valueDate))!);

    public Task<ServiceResult<FuturesItiSignalV2ReadModel[]>> GetFuturesItiTrendDirectionChangedSignalsAsync(
        string contractId, DateOnly valueDate, TimeFrameType timePeriod)
        => ExecuteAsync<FuturesItiSignalV2ReadModel[]>(GetFuturesItiTrendDirectionChangedSignalsQuery.ErrorId,
            async () => [.. await _dbFactory.MarketDataDb.GetFuturesItiTrendDirectionChangedSignalsAsync(
                contractId, valueDate)]);

    public Task<ServiceResult<FuturesItiSignalDataReadModel>> GetFuturesItiSignalDataAsync(
        string contractId, DateOnly valueDate, TimeFrameType timePeriod)
        => ExecuteAsync(GetFuturesItiSignalDataQuery.ErrorId, async () =>
        {
            var db = _dbFactory.MarketDataDb;
            return new FuturesItiSignalDataReadModel(
                await db.GetLastFuturesItiSignalTrendDirectionChangeAsync(contractId, valueDate),
                await db.GetLastFuturesItiSignalTrendExtremeChangeAsync(contractId, valueDate),
                await db.GetLastFuturesItiSignalTrendReversalChangeAsync(contractId, valueDate));
        });

    public Task<ServiceResult<FuturesItiMDIDistributionReadModel>> GetFuturesItiMDIDistributionAsync(
        string contractId, DateOnly valueDate)
        => ExecuteAsync(GetFuturesItiMDIDistributionQuery.ErrorId, async () =>
            new FuturesItiMDIDistributionReadModel(
                [.. await _dbFactory.MarketDataDb.GetFuturesItiSignalMDIAsync(contractId, valueDate)]));

    public Task<ServiceResult<FuturesItiMDIDistributionReadModel>> GetFuturesItiMDIDistributionByTrendAsync(
        string contractId, DateOnly valueDate)
        => ExecuteAsync(GetFuturesItiMDIDistributionByTrendQuery.ErrorId, async () =>
        {
            var signal = await _dbFactory.MarketDataDb.GetLastFuturesItiSignalAsync(contractId, valueDate);
            var values = await GetFuturesItiSignalMDIByTrendCoreAsync(
                contractId, valueDate, signal?.IntrinsicTimeGroupId ?? 0);
            return new FuturesItiMDIDistributionReadModel(values);
        });

    public Task<ServiceResult<FuturesItiSignalMDIV2ReadModel[]>> GetFuturesItiSignalMDIAsync(
        string contractId, DateOnly valueDate)
        => ExecuteAsync<FuturesItiSignalMDIV2ReadModel[]>(GetFuturesItiSignalMDIQuery.ErrorId,
            async () => [.. await _dbFactory.MarketDataDb.GetFuturesItiSignalMDIAsync(contractId, valueDate)]);

    public Task<ServiceResult<FuturesItiSignalMDIV2ReadModel[]>> GetFuturesItiSignalMDIByTrendAsync(
        string contractId, DateOnly valueDate, int groupId)
        => ExecuteAsync(GetFuturesItiSignalMDIByTrendQuery.ErrorId,
            async () => await GetFuturesItiSignalMDIByTrendCoreAsync(contractId, valueDate, groupId));

    public Task<ServiceResult<FuturesAtrSignalReadModel>> GetFuturesAtrSignalAsync(
        string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength)
        => ExecuteAsync(GetFuturesAtrSignalQuery.ErrorId,
            async () => (await _dbFactory.MarketDataDb.GetLastFuturesAtrSignalAsync(
                contractId, valueDate, timePeriod, periodLength))!);

    public Task<ServiceResult<FuturesAdxSignalReadModel>> GetFuturesAdxSignalAsync(
        string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength)
        => ExecuteAsync(GetFuturesAdxSignalQuery.ErrorId,
            async () => (await _dbFactory.MarketDataDb.GetLastFuturesAdxSignalAsync(
                contractId, valueDate, timePeriod, periodLength))!);

    public Task<ServiceResult<FuturesMacdSignalReadModel>> GetFuturesMacdSignalAsync(
        string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength)
        => ExecuteAsync(GetFuturesMacdSignalQuery.ErrorId,
            async () => (await _dbFactory.MarketDataDb.GetLastFuturesMacdSignalAsync(
                contractId, valueDate, timePeriod, periodLength))!);

    async Task<FuturesItiSignalMDIV2ReadModel[]> GetFuturesItiSignalMDIByTrendCoreAsync(
        string contractId, DateOnly valueDate, int groupId)
    {
        var db = _dbFactory.MarketDataDb;
        var upTrend = await db.GetFuturesItiSignalMDIByTrendAsync(
            contractId, valueDate, IntrinsicTimeTrendType.UpTrend, groupId);
        var downTrend = await db.GetFuturesItiSignalMDIByTrendAsync(
            contractId, valueDate, IntrinsicTimeTrendType.DownTrend, groupId);
        return [.. upTrend, .. downTrend];
    }

    static async Task<ServiceResult<T>> ExecuteAsync<T>(int errorId, Func<Task<T>> query)
    {
        try
        {
            return new ServiceOk<T>(await query());
        }
        catch (Exception ex)
        {
            return new ServiceFailed<T>(errorId, ex.Message);
        }
    }
}
