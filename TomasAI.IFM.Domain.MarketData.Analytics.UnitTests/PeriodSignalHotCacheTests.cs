using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Event;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Event;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Event;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests;

public sealed class PeriodSignalHotCacheTests
{
    const string ContractId = "ESU26-HOT-CACHE";
    static readonly DateOnly ValueDate = new(2026, 8, 14);
    static readonly DateTimeOffset EventTimestamp = new(2026, 8, 14, 15, 30, 45, TimeSpan.Zero);

    [Fact]
    public async Task AtrStarted_UsesHotCachePriceAndFeedTimestamp()
    {
        var entityId = new FuturesAtrSignalEntityId(ContractId, ValueDate, TimeFrameType.TenSeconds, 14);
        var started = new FuturesAtrSignalStartedEvent { EntityId = entityId };
        var stopped = new FuturesAtrSignalStoppedEvent { EntityId = entityId };
        var marketDataApi = CreateMarketDataApi();
        var commandApi = Substitute.For<IActorMarketDataAnalyticsCommandApi>();
        var sent = ConfigureSend(commandApi, static (api, completion) =>
            api.GenerateFuturesAtrSignalAsync(Arg.Any<FuturesAtrSignalId>(), Arg.Any<decimal>())
                .Returns(_ => Complete(completion)));

        try
        {
            await started.ExecuteAsync(Context(), commandApi, marketDataApi, Status(), Logger());
            await sent.Task.WaitAsync(TimeSpan.FromSeconds(1));

            await commandApi.Received(1).GenerateFuturesAtrSignalAsync(
                Arg.Is<FuturesAtrSignalId>(id =>
                    id.ContractId == ContractId
                    && id.Timestamp == TimeOnly.FromDateTime(EventTimestamp.UtcDateTime)),
                6425.25m);
        }
        finally
        {
            await stopped.ExecuteAsync(Context(), Status(), Logger());
        }
    }

    [Fact]
    public async Task MacdStarted_UsesHotCachePriceAndFeedTimestamp()
    {
        var entityId = new FuturesMacdSignalEntityId(ContractId, ValueDate, TimeFrameType.TenSeconds, 26);
        var started = new FuturesMacdSignalStartedEvent { EntityId = entityId };
        var stopped = new FuturesMacdSignalStoppedEvent { EntityId = entityId };
        var marketDataApi = CreateMarketDataApi();
        var commandApi = Substitute.For<IActorMarketDataAnalyticsCommandApi>();
        var sent = ConfigureSend(commandApi, static (api, completion) =>
            api.GenerateFuturesMacdSignalAsync(Arg.Any<FuturesMacdSignalId>(), Arg.Any<decimal>())
                .Returns(_ => Complete(completion)));

        try
        {
            await started.ExecuteAsync(Context(), commandApi, marketDataApi, Status(), Logger());
            await sent.Task.WaitAsync(TimeSpan.FromSeconds(1));

            await commandApi.Received(1).GenerateFuturesMacdSignalAsync(
                Arg.Is<FuturesMacdSignalId>(id =>
                    id.ContractId == ContractId
                    && id.Timestamp == TimeOnly.FromDateTime(EventTimestamp.UtcDateTime)),
                6425.25m);
        }
        finally
        {
            await stopped.ExecuteAsync(Context(), Status(), Logger());
        }
    }

    [Fact]
    public async Task AdxStarted_UsesHotCachePriceAndFeedTimestamp()
    {
        var entityId = new FuturesAdxSignalEntityId(ContractId, ValueDate, TimeFrameType.TenSeconds, 14);
        var started = new FuturesAdxSignalStartedEvent { EntityId = entityId };
        var stopped = new FuturesAdxSignalStoppedEvent { EntityId = entityId };
        var marketDataApi = CreateMarketDataApi();
        var commandApi = Substitute.For<IActorMarketDataAnalyticsCommandApi>();
        var sent = ConfigureSend(commandApi, static (api, completion) =>
            api.GenerateFuturesAdxSignalAsync(Arg.Any<FuturesAdxSignalId>(), Arg.Any<decimal>())
                .Returns(_ => Complete(completion)));

        try
        {
            await started.ExecuteAsync(Context(), commandApi, marketDataApi, Status(), Logger());
            await sent.Task.WaitAsync(TimeSpan.FromSeconds(1));

            await commandApi.Received(1).GenerateFuturesAdxSignalAsync(
                Arg.Is<FuturesAdxSignalId>(id =>
                    id.ContractId == ContractId
                    && id.Timestamp == TimeOnly.FromDateTime(EventTimestamp.UtcDateTime)),
                6425.25m);
        }
        finally
        {
            await stopped.ExecuteAsync(Context(), Status(), Logger());
        }
    }

    static IMarketDataApi CreateMarketDataApi()
    {
        var snapshot = new FuturesMarketPriceSnapshot(
            ContractId,
            101,
            7,
            AssetTypeId.Futures,
            ValueDate,
            null,
            new FuturesMarketTradeSnapshot(6425.25m, 4, 9001, EventTimestamp, EventTimestamp));
        var marketDataApi = Substitute.For<IMarketDataApi>();
        marketDataApi.IsTickDataStreamActive(ContractId).Returns(true);
        marketDataApi.TryGetLastTickPrice(ContractId, out Arg.Any<FuturesMarketPriceSnapshot>())
            .Returns(callInfo =>
            {
                callInfo[1] = snapshot;
                return true;
            });
        return marketDataApi;
    }

    static TaskCompletionSource ConfigureSend(
        IActorMarketDataAnalyticsCommandApi commandApi,
        Action<IActorMarketDataAnalyticsCommandApi, TaskCompletionSource> configure)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        configure(commandApi, completion);
        return completion;
    }

    static ServiceResult<GuidResult> Complete(TaskCompletionSource completion)
    {
        completion.TrySetResult();
        return new ServiceOk<GuidResult>(new GuidResult(Guid.NewGuid()));
    }

    static IEventActorContext Context() => Substitute.For<IEventActorContext>();
    static IStatusConsoleWriter Status() => Substitute.For<IStatusConsoleWriter>();
    static ILogger Logger() => Substitute.For<ILogger>();
}
