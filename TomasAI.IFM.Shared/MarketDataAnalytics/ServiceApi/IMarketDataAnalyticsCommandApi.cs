using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.ServiceApi;

public interface IMarketDataAnalyticsCommandApi
{
    Task<ServiceResult<Guid>> UpdateFuturesTradeSignalAsync(
        FuturesEodDataV2ReadModel futuresEodData, 
        FuturesRsiSignalReadModel futuresRsiSignal,
        FuturesTdiSignalReadModel futuresTdiSignal,
        FuturesItiSignalDataReadModel futuresItiSignalData,
        decimal vixFuturesPrice);
    Task<ServiceResult<Guid>> GenerateFuturesRsiSignalAsync(FuturesEodDataV2ReadModel futuresEodData, TradeTimePeriodType timePeriod, int periodLength);
    Task<ServiceResult<Guid>> GenerateFuturesRsiDailySignalAsync(FuturesEodDataV2ReadModel futuresEodData, TradeTimePeriodType timePeriod, int periodLength);
    Task<ServiceResult<Guid>> StartFuturesRsiSignalAsync(FuturesRsiSignalEntityId entityId);
    Task<ServiceResult<Guid>> StopFuturesRsiSignalAsync(FuturesRsiSignalEntityId entityId);
    Task<ServiceResult<Guid>> GenerateFuturesTdiSignalAsync(FuturesTdiSignalId futuresTdiSignalId, FuturesRsiSignalReadModel[] futuresRsiSignals);
    Task<ServiceResult<Guid>> GenerateFuturesItiSignalAsync(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod, DateTime timestamp, double futuresPrice, double vixFuturesPrice);
    Task<ServiceResult<Guid>> SetFuturesItiSignalHoldTradeAsync(FuturesItiSignalId itiSignalId);
    Task<ServiceResult<Guid>> ClearFuturesItiSignalHoldTradeAsync(FuturesItiSignalId itiSignalId);
    Task<ServiceResult<Guid>> GenerateFuturesAtrSignalAsync(FuturesAtrSignalId futuresAtrSignalId, FuturesItiSignalV2ReadModel[] futuresItiSignals);
    Task<ServiceResult<Guid>> GenerateFuturesAtrSignalFromIntraDayDataAsync(FuturesAtrSignalId futuresAtrSignalId, FuturesIntraDayDataReadModel[] futuresIntraDayData);
    Task<ServiceResult<Guid>> GenerateFuturesAdxSignalAsync(FuturesAdxSignalId futuresAdxSignalId, decimal futuresPrice);
    Task<ServiceResult<Guid>> GenerateFuturesMacdSignalAsync(FuturesMacdSignalId futuresMacdSignalId, decimal futuresPrice);
}
