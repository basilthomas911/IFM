using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;

/// <summary>
/// Defines Market Data Analytics queries for direct, in-process use by domain actors.
/// </summary>
/// <remarks>Implementations must not use HTTP, NATS, or actor messaging.</remarks>
public interface IActorMarketDataAnalyticsQueryApi : IMarketDataAnalyticsQueryApi
{
    Task<ServiceResult<FuturesAdxSignalReadModel>> GetFuturesAdxDailySignalAsync(
        string contractId,
        TimeFrameType timePeriod,
        int periodLength);

    Task<ServiceResult<FuturesAtrSignalReadModel>> GetFuturesAtrDailySignalAsync(
        string contractId,
        TimeFrameType timePeriod,
        int periodLength);

    Task<ServiceResult<FuturesMacdSignalReadModel>> GetFuturesMacdDailySignalAsync(
        string contractId,
        TimeFrameType timePeriod,
        int periodLength);

    Task<ServiceResult<FuturesRsiSignalReadModel>> GetFuturesRsiDailySignalAsync(
        string contractId,
        TimeFrameType timePeriod,
        int periodLength);
}
