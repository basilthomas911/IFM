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

    public async Task<ServiceResult<FuturesTrendDirectionReadModel>> GetFuturesTrendDirectionFromRSISignalAsync(
        string contractId, DateOnly valueDate, DateTime timestamp, int loopbackInterval,
        DateTime startTime, DateTime endTime)
    {
        try
        {
            FuturesTrendDirectionReadModel result =
                await _dbFactory.MarketDataDb.GetFuturesTrendDirectionFromRSISignalAsync(
                    contractId,
                    valueDate,
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

    public async Task<ServiceResult<FuturesTdiSignalReadModel>> GetFuturesTdiSignalAsync(
        string contractId, DateOnly valueDate)
    {
        try
        {
            FuturesTdiSignalReadModel result =
                (await _dbFactory.MarketDataDb.GetLastFuturesTdiSignalAsync(contractId, valueDate))!;
            return new ServiceOk<FuturesTdiSignalReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesTdiSignalReadModel>(GetFuturesTdiSignalQuery.ErrorId, ex.Message);
        }
    }

    public async Task<ServiceResult<FuturesItiSignalV2ReadModel>> GetFuturesItiSignalAsync(
        string contractId, DateOnly valueDate, TimeFrameType timePeriod)
    {
        try
        {
            FuturesItiSignalV2ReadModel result =
                (await _dbFactory.MarketDataDb.GetLastFuturesItiSignalAsync(contractId, valueDate))!;
            return new ServiceOk<FuturesItiSignalV2ReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesItiSignalV2ReadModel>(GetFuturesItiSignalQuery.ErrorId, ex.Message);
        }
    }

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

    public async Task<ServiceResult<FuturesItiSignalDataReadModel>> GetFuturesItiSignalDataAsync(
        string contractId, DateOnly valueDate, TimeFrameType timePeriod)
    {
        try
        {
            var db = _dbFactory.MarketDataDb;
            var result = new FuturesItiSignalDataReadModel(
                await db.GetLastFuturesItiSignalTrendDirectionChangeAsync(contractId, valueDate),
                await db.GetLastFuturesItiSignalTrendExtremeChangeAsync(contractId, valueDate),
                await db.GetLastFuturesItiSignalTrendReversalChangeAsync(contractId, valueDate));
            return new ServiceOk<FuturesItiSignalDataReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesItiSignalDataReadModel>(
                GetFuturesItiSignalDataQuery.ErrorId,
                ex.Message);
        }
    }

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

    public async Task<ServiceResult<FuturesMacdSignalReadModel>> GetFuturesMacdSignalAsync(
        string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength)
    {
        try
        {
            FuturesMacdSignalReadModel result =
                (await _dbFactory.MarketDataDb.GetLastFuturesMacdSignalAsync(
                    contractId,
                    valueDate,
                    timePeriod,
                    periodLength))!;
            return new ServiceOk<FuturesMacdSignalReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesMacdSignalReadModel>(GetFuturesMacdSignalQuery.ErrorId, ex.Message);
        }
    }

    public async Task<ServiceResult<FuturesMacdSignalReadModel>> GetFuturesMacdDailySignalAsync(
        string contractId, TimeFrameType timePeriod, int periodLength)
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
}
