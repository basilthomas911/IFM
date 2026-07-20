using System;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.ServiceApi;

public interface IMarketDataAnalyticsQueryApi
{
    Task<ServiceResult<FuturesTradeSignalV2ReadModel>> GetFuturesTradeSignalAsync(string contractId, DateOnly valueDate);
    Task<ServiceResult<FuturesTradeSignalV2ReadModel>> GetLastFuturesTradeSignalAsync();
    Task<ServiceResult<FuturesTradeSignalV2ReadModel>> GetFuturesTradeSignalBySymbolAsync(string symbol, DateOnly valueDate);
    Task<ServiceResult<FuturesTradeSignalId[]>> GetFuturesTradeSignalIdsAsync(DateOnly valueDate);
    /// <summary>
    /// Gets the futures RSI signal for a contract, time period, and period length.
    /// </summary>
    Task<ServiceResult<FuturesRsiSignalReadModel>> GetFuturesRsiSignalAsync(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod, int periodLength);
    Task<ServiceResult<FuturesTrendDirectionReadModel>> GetFuturesTrendDirectionFromRSISignalAsync(
        string contractId, DateOnly valueDate, DateTime timestamp, int loopbackInterval, DateTime startTime, DateTime endTime);
    Task<ServiceResult<FuturesTdiSignalReadModel>> GetFuturesTdiSignalAsync(string contractId, DateOnly valueDate);
    Task<ServiceResult<FuturesItiSignalV2ReadModel>> GetFuturesItiSignalAsync(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod);
    Task<ServiceResult<FuturesItiSignalV2ReadModel[]>> GetFuturesItiTrendDirectionChangedSignalsAsync(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod);
    Task<ServiceResult<FuturesItiSignalDataReadModel>> GetFuturesItiSignalDataAsync(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod);
    Task<ServiceResult<FuturesItiMDIDistributionReadModel>> GetFuturesItiMDIDistributionAsync(string contractId, DateOnly valueDate);
    Task<ServiceResult<FuturesItiMDIDistributionReadModel>> GetFuturesItiMDIDistributionByTrendAsync(string contractId, DateOnly valueDate);
    Task<ServiceResult<FuturesItiSignalMDIV2ReadModel[]>> GetFuturesItiSignalMDIAsync(string contractId, DateOnly valueDate);
    Task<ServiceResult<FuturesItiSignalMDIV2ReadModel[]>> GetFuturesItiSignalMDIByTrendAsync(string contractId, DateOnly valueDate, int groupId);
    Task<ServiceResult<FuturesAtrSignalReadModel>> GetFuturesAtrSignalAsync(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod, int periodLength);
    Task<ServiceResult<FuturesAdxSignalReadModel>> GetFuturesAdxSignalAsync(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod, int periodLength);
    Task<ServiceResult<FuturesMacdSignalReadModel>> GetFuturesMacdSignalAsync(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod, int periodLength);
}
