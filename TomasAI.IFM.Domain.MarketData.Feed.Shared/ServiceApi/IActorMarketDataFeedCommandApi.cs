using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;

/// <summary>
/// Defines NATS-backed Market Data Feed commands intended for use by domain event actors.
/// </summary>
public interface IActorMarketDataFeedCommandApi
{
    ValueTask<ServiceResult<GuidResult>> TurnTradeLiveFeedOffAsync(
        Guid commandId,
        int orderId,
        int tradeId,
        DateOnly valueDate);

    ValueTask<ServiceResult<GuidResult>> TurnTradeLiveFeedOnAsync(
        Guid commandId,
        int orderId,
        int tradeId,
        DateOnly valueDate);

    ValueTask<ServiceResult<GuidResult>> StopFuturesBarDataStreamingAsync(DateOnly valueDate);

    ValueTask<ServiceResult<GuidResult>> StopFuturesOptionTickDataStreamingAsync(
        Guid commandId,
        FuturesOptionTickEntityId entityId,
        string contractId);

    ValueTask<ServiceResult<GuidResult>> StartFuturesTickDataStreamingAsync(
        FuturesContractV2ReadModel futuresContract,
        DateOnly valueDate,
        bool resetStream,
        FuturesDataId entityId);

    ValueTask<ServiceResult<GuidResult>> StartFuturesBarDataStreamingAsync(
        FuturesContractV2ReadModel[] futuresContracts,
        DateOnly valueDate,
        FuturesBarDataStreamingId entityId);

    ValueTask<ServiceResult<GuidResult>> StartFuturesOptionTickDataStreamingAsync(
        Guid commandId,
        FuturesOptionTickEntityId entityId,
        FuturesOptionContractReadModel contract,
        FuturesContractV2ReadModel baseContract,
        DateOnly valueDate,
        DateOnly maturityDate,
        double riskFreeRate);

    ValueTask<ServiceResult<GuidResult>> InsertFuturesBarDataAsync(FuturesBarDataReadModel futuresBarData);
    ValueTask<ServiceResult<GuidResult>> DeleteStreamingRequestIdAsync(FeedId feedId);

    ValueTask<ServiceResult<GuidResult>> InsertFuturesEodDataAsync(
        DateOnly valueDate,
        FuturesTickDataV2ReadModel futuresTickData,
        FuturesContractV2ReadModel futuresContract,
        FuturesEodDataV2ReadModel eodDataToday,
        ICollection<FuturesEodDataV2ReadModel> eodDataRange,
        NormalCurveTableReadModel normalCurveData,
        int windowSize,
        ICollection<VixFuturesEodDataReadModel> vixEodData);

    ValueTask<ServiceResult<GuidResult>> InsertVixFuturesEodDataAsync(FuturesTickDataV2ReadModel futuresTickData);

    ValueTask<ServiceResult<GuidResult>> InsertFuturesTickDataAsync(
        FuturesContractV2ReadModel futuresContract,
        FuturesTickDataV2ReadModel futuresTickData);

    ValueTask<ServiceResult<GuidResult>> InsertFuturesOptionTickPriceDataAsync(
        FuturesContractV2ReadModel underlyingContract,
        FuturesOptionTickDataV2ReadModel optionContract);

    ValueTask<ServiceResult<GuidResult>> InsertFuturesOptionTickDataAsync(
        FuturesContractV2ReadModel underlyingContract,
        FuturesOptionTickDataV2ReadModel optionContract);
}

public interface IActorMarketDataFeedCommandApiFactory
{
    IActorMarketDataFeedCommandApi Create(IEventActorContext context);
}
