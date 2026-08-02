using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;

/// <summary>
/// Defines NATS-backed Market Data Analytics commands intended for use by domain event actors.
/// </summary>
public interface IActorMarketDataAnalyticsCommandApi
{
    ValueTask<ServiceResult<GuidResult>> GenerateFuturesRsiSignalAsync(
        FuturesRsiSignalId signalId,
        decimal futuresPrice);

    ValueTask<ServiceResult<GuidResult>> GenerateFuturesTdiSignalAsync(
        FuturesTdiSignalId signalId,
        FuturesRsiSignalReadModel[] futuresRsiSignals,
        TimeFrameType timePeriod);

    ValueTask<ServiceResult<GuidResult>> GenerateFuturesMacdSignalAsync(
        FuturesMacdSignalId signalId,
        decimal futuresPrice);

    ValueTask<ServiceResult<GuidResult>> GenerateFuturesAdxSignalAsync(
        FuturesAdxSignalId signalId,
        decimal futuresPrice);

    ValueTask<ServiceResult<GuidResult>> GenerateFuturesAtrSignalAsync(
        FuturesAtrSignalId signalId,
        decimal futuresPrice);

    ValueTask<ServiceResult<GuidResult>> UpdateFuturesTradeSignalAsync(
        FuturesEodDataV2ReadModel futuresEodData,
        FuturesRsiSignalReadModel? futuresRsiSignal,
        FuturesTdiSignalReadModel? futuresTdiSignal,
        FuturesItiSignalDataReadModel? futuresItiSignalData,
        decimal vixFuturesPrice,
        TimeFrameType timePeriod);

    ValueTask<ServiceResult<GuidResult>> GenerateFuturesItiSignalAsync(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        DateTime timestamp,
        double futuresPrice,
        double vixFuturesPrice);
}

public interface IActorMarketDataAnalyticsCommandApiFactory
{
    IActorMarketDataAnalyticsCommandApi Create(IEventActorContext context);
}
