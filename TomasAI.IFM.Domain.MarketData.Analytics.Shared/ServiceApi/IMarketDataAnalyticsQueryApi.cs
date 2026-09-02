using System;
using System.Threading.Tasks;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;

public interface IMarketDataAnalyticsQueryApi
{
    Task<ServiceResult<MarketOutlookReadModel>> GetMarketOutlookSnapshotAsync(
        string contractId,
        DateOnly valueDate,
        bool loadPersistedBaseline = false);
    Task<ServiceResult<FuturesTradeSignalV2ReadModel>> GetFuturesTradeSignalAsync(string contractId, DateOnly valueDate);
    Task<ServiceResult<FuturesTradeSignalV2ReadModel>> GetLastFuturesTradeSignalAsync();
    Task<ServiceResult<FuturesTradeSignalV2ReadModel>> GetFuturesTradeSignalBySymbolAsync(string symbol, DateOnly valueDate);
    Task<ServiceResult<FuturesTradeSignalId[]>> GetFuturesTradeSignalIdsAsync(DateOnly valueDate);
    /// <summary>
    /// Gets the futures RSI signal for a contract, time period, and period length.
    /// </summary>
    Task<ServiceResult<FuturesRsiSignalReadModel>> GetFuturesRsiSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength);
    Task<ServiceResult<FuturesTrendDirectionReadModel>> GetFuturesTrendDirectionFromRSISignalAsync(
        string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength,
        DateTime timestamp, int loopbackInterval, DateTime startTime, DateTime endTime);
    Task<ServiceResult<FuturesTdiSignalReadModel>> GetFuturesTdiSignalAsync(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod = TimeFrameType.OneMinute,
        string configurationId = FuturesTdiConfiguration.StandardConfigurationId);
    Task<ServiceResult<FuturesItiSignalV2ReadModel>> GetFuturesItiSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod);
    Task<ServiceResult<FuturesItiSignalV2ReadModel[]>> GetFuturesItiSignalHistoryAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod);
    Task<ServiceResult<FuturesItiSignalV2ReadModel[]>> GetFuturesItiTrendDirectionChangedSignalsAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod);
    Task<ServiceResult<FuturesItiSignalDataReadModel>> GetFuturesItiSignalDataAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod);
    Task<ServiceResult<FuturesItiMDIDistributionReadModel>> GetFuturesItiMDIDistributionAsync(string contractId, DateOnly valueDate);
    Task<ServiceResult<FuturesItiMDIDistributionReadModel>> GetFuturesItiMDIDistributionByTrendAsync(string contractId, DateOnly valueDate);
    Task<ServiceResult<FuturesItiSignalMDIV2ReadModel[]>> GetFuturesItiSignalMDIAsync(string contractId, DateOnly valueDate);
    Task<ServiceResult<FuturesItiSignalMDIV2ReadModel[]>> GetFuturesItiSignalMDIByTrendAsync(string contractId, DateOnly valueDate, int groupId);
    Task<ServiceResult<FuturesAtrSignalReadModel>> GetFuturesAtrSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength);
    Task<ServiceResult<FuturesAdxSignalReadModel>> GetFuturesAdxSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength);
    Task<ServiceResult<FuturesMacdSignalReadModel>> GetFuturesMacdSignalAsync(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        int signalEmaPeriod = FuturesMacdConfiguration.ConventionalSignalEmaPeriod,
        int fastEmaPeriod = FuturesMacdConfiguration.ConventionalFastEmaPeriod,
        int slowEmaPeriod = FuturesMacdConfiguration.ConventionalSlowEmaPeriod);
}
