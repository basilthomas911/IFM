using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;

public interface IMarketDataAnalyticsCommandApi
{
    Task<ServiceResult<Guid>> UpdateFuturesTradeSignalAsync(
        FuturesEodDataV2ReadModel futuresEodData, 
        FuturesRsiSignalReadModel futuresRsiSignal,
        FuturesTdiSignalReadModel futuresTdiSignal,
        FuturesItiSignalDataReadModel futuresItiSignalData,
        decimal vixFuturesPrice);
    Task<ServiceResult<Guid>> GenerateFuturesRsiSignalAsync(FuturesEodDataV2ReadModel futuresEodData, TimeFrameType timePeriod, int periodLength);
    Task<ServiceResult<Guid>> GenerateFuturesRsiDailySignalAsync(FuturesEodDataV2ReadModel futuresEodData, TimeFrameType timePeriod, int periodLength);
    Task<ServiceResult<Guid>> StartFuturesRsiSignalAsync(FuturesRsiSignalEntityId entityId);
    Task<ServiceResult<Guid>> StopFuturesRsiSignalAsync(FuturesRsiSignalEntityId entityId);
    Task<ServiceResult<Guid>> StartFuturesMacdSignalAsync(FuturesMacdSignalEntityId entityId);
    Task<ServiceResult<Guid>> StopFuturesMacdSignalAsync(FuturesMacdSignalEntityId entityId);
    Task<ServiceResult<Guid>> StartFuturesAdxSignalAsync(FuturesAdxSignalEntityId entityId);
    Task<ServiceResult<Guid>> StopFuturesAdxSignalAsync(FuturesAdxSignalEntityId entityId);
    Task<ServiceResult<Guid>> StartFuturesAtrSignalAsync(FuturesAtrSignalEntityId entityId);
    Task<ServiceResult<Guid>> StopFuturesAtrSignalAsync(FuturesAtrSignalEntityId entityId);
    Task<ServiceResult<Guid>> GenerateFuturesTdiSignalAsync(FuturesTdiSignalId futuresTdiSignalId, FuturesRsiSignalReadModel[] futuresRsiSignals);
    Task<ServiceResult<Guid>> GenerateFuturesItiSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, DateTime timestamp, double futuresPrice, double vixFuturesPrice);
    Task<ServiceResult<Guid>> SetFuturesItiSignalHoldTradeAsync(FuturesItiSignalId itiSignalId);
    Task<ServiceResult<Guid>> ClearFuturesItiSignalHoldTradeAsync(FuturesItiSignalId itiSignalId);
    Task<ServiceResult<Guid>> GenerateFuturesAtrSignalAsync(FuturesAtrSignalId futuresAtrSignalId, FuturesItiSignalV2ReadModel[] futuresItiSignals);
    Task<ServiceResult<Guid>> GenerateFuturesAtrSignalFromIntraDayDataAsync(FuturesAtrSignalId futuresAtrSignalId, FuturesIntraDayDataReadModel[] futuresIntraDayData);
    Task<ServiceResult<Guid>> GenerateFuturesAdxSignalAsync(FuturesAdxSignalId futuresAdxSignalId, decimal futuresPrice);
    Task<ServiceResult<Guid>> GenerateFuturesMacdSignalAsync(FuturesMacdSignalId futuresMacdSignalId, decimal futuresPrice);
}
