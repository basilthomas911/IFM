using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;

/// <summary>
/// Defines Market Data Analytics queries for direct, in-process use by domain actors.
/// </summary>
/// <remarks>Implementations must not use HTTP, NATS, or actor messaging.</remarks>
public interface IActorMarketDataAnalyticsQueryApi : IMarketDataAnalyticsQueryApi
{
    Task<ServiceResult<FuturesTradeSignalV2ReadModel>> GetFuturesTradeSignalAsync(
        string contractId, DateOnly valueDate, CancellationToken cancellationToken);
    Task<ServiceResult<FuturesTradeSignalV2ReadModel>> GetLastFuturesTradeSignalAsync(
        CancellationToken cancellationToken);
    Task<ServiceResult<FuturesTradeSignalV2ReadModel>> GetFuturesTradeSignalBySymbolAsync(
        string symbol, DateOnly valueDate, CancellationToken cancellationToken);
    Task<ServiceResult<FuturesTradeSignalId[]>> GetFuturesTradeSignalIdsAsync(
        DateOnly valueDate, CancellationToken cancellationToken);
    Task<ServiceResult<FuturesRsiSignalReadModel>> GetFuturesRsiSignalAsync(
        string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength,
        CancellationToken cancellationToken);
    Task<ServiceResult<FuturesTrendDirectionReadModel>> GetFuturesTrendDirectionFromRSISignalAsync(
        string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength,
        DateTime timestamp, int loopbackInterval,
        DateTime startTime, DateTime endTime, CancellationToken cancellationToken);
    Task<ServiceResult<FuturesTdiSignalReadModel>> GetFuturesTdiSignalAsync(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        string configurationId,
        CancellationToken cancellationToken);
    Task<ServiceResult<FuturesItiSignalV2ReadModel>> GetFuturesItiSignalAsync(
        string contractId, DateOnly valueDate, TimeFrameType timePeriod,
        CancellationToken cancellationToken);
    Task<ServiceResult<FuturesItiSignalV2ReadModel[]>> GetFuturesItiTrendDirectionChangedSignalsAsync(
        string contractId, DateOnly valueDate, TimeFrameType timePeriod,
        CancellationToken cancellationToken);
    Task<ServiceResult<FuturesItiSignalDataReadModel>> GetFuturesItiSignalDataAsync(
        string contractId, DateOnly valueDate, TimeFrameType timePeriod,
        CancellationToken cancellationToken);
    Task<ServiceResult<FuturesItiMDIDistributionReadModel>> GetFuturesItiMDIDistributionAsync(
        string contractId, DateOnly valueDate, CancellationToken cancellationToken);
    Task<ServiceResult<FuturesItiMDIDistributionReadModel>> GetFuturesItiMDIDistributionByTrendAsync(
        string contractId, DateOnly valueDate, CancellationToken cancellationToken);
    Task<ServiceResult<FuturesItiSignalMDIV2ReadModel[]>> GetFuturesItiSignalMDIAsync(
        string contractId, DateOnly valueDate, CancellationToken cancellationToken);
    Task<ServiceResult<FuturesItiSignalMDIV2ReadModel[]>> GetFuturesItiSignalMDIByTrendAsync(
        string contractId, DateOnly valueDate, int groupId, CancellationToken cancellationToken);
    Task<ServiceResult<FuturesAtrSignalReadModel>> GetFuturesAtrSignalAsync(
        string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength,
        CancellationToken cancellationToken);
    Task<ServiceResult<FuturesAdxSignalReadModel>> GetFuturesAdxSignalAsync(
        string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength,
        CancellationToken cancellationToken);
    Task<ServiceResult<FuturesMacdSignalReadModel>> GetFuturesMacdSignalAsync(
        string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength,
        CancellationToken cancellationToken);
    Task<ServiceResult<FuturesMacdSignalReadModel>> GetFuturesMacdSignalAsync(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        int signalEmaPeriod,
        int fastEmaPeriod,
        int slowEmaPeriod,
        CancellationToken cancellationToken);

    Task<ServiceResult<FuturesAdxSignalReadModel>> GetFuturesAdxDailySignalAsync(
        string contractId,
        TimeFrameType timePeriod,
        int periodLength);
    Task<ServiceResult<FuturesAdxSignalReadModel>> GetFuturesAdxDailySignalAsync(
        string contractId, TimeFrameType timePeriod, int periodLength,
        CancellationToken cancellationToken);

    Task<ServiceResult<FuturesAtrSignalReadModel>> GetFuturesAtrDailySignalAsync(
        string contractId,
        TimeFrameType timePeriod,
        int periodLength);
    Task<ServiceResult<FuturesAtrSignalReadModel>> GetFuturesAtrDailySignalAsync(
        string contractId, TimeFrameType timePeriod, int periodLength,
        CancellationToken cancellationToken);

    Task<ServiceResult<FuturesMacdSignalReadModel>> GetFuturesMacdDailySignalAsync(
        string contractId,
        TimeFrameType timePeriod,
        int periodLength);
    Task<ServiceResult<FuturesMacdSignalReadModel>> GetFuturesMacdDailySignalAsync(
        string contractId,
        TimeFrameType timePeriod,
        int signalEmaPeriod,
        int fastEmaPeriod,
        int slowEmaPeriod);
    Task<ServiceResult<FuturesMacdSignalReadModel>> GetFuturesMacdDailySignalAsync(
        string contractId, TimeFrameType timePeriod, int periodLength,
        CancellationToken cancellationToken);
    Task<ServiceResult<FuturesMacdSignalReadModel>> GetFuturesMacdDailySignalAsync(
        string contractId,
        TimeFrameType timePeriod,
        int signalEmaPeriod,
        int fastEmaPeriod,
        int slowEmaPeriod,
        CancellationToken cancellationToken);

    Task<ServiceResult<FuturesRsiSignalReadModel>> GetFuturesRsiDailySignalAsync(
        string contractId,
        TimeFrameType timePeriod,
        int periodLength);
    Task<ServiceResult<FuturesRsiSignalReadModel>> GetFuturesRsiDailySignalAsync(
        string contractId, TimeFrameType timePeriod, int periodLength,
        CancellationToken cancellationToken);
}
