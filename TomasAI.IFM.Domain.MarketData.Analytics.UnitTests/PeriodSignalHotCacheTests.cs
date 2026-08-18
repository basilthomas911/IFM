using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Event;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Event;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Event;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests;

public sealed class PeriodSignalHotCacheTests
{
    const string ContractId = "ESU26-HOT-CACHE";
    static readonly DateOnly ValueDate = new(2026, 8, 14);
    static readonly DateTimeOffset EventTimestamp = new(2026, 8, 14, 15, 30, 45, TimeSpan.Zero);

    [Fact]
    public async Task RsiStarted_PublishesRealtimeSampleFromHotCache()
    {
        var entityId = new FuturesRsiSignalEntityId(ContractId, ValueDate, TimeFrameType.TenSeconds, 13);
        var started = new FuturesRsiSignalStartedEvent { EntityId = entityId };
        var stopped = new FuturesRsiSignalStoppedEvent { EntityId = entityId };
        var marketDataApi = CreateMarketDataApi();
        var commandApi = Substitute.For<IActorMarketDataAnalyticsCommandApi>();
        var context = Context();
        var sent = Capture<FuturesRsiSignalSampledRealtimeEvent, FuturesRsiSignalEntityId>(context);

        try
        {
            await started.ExecuteAsync(context, commandApi, marketDataApi, Status(), Logger());
            await sent.Task.WaitAsync(TimeSpan.FromSeconds(1));

            await context.Received(1).SendAsync<FuturesRsiSignalSampledRealtimeEvent, FuturesRsiSignalEntityId>(
                Arg.Is<FuturesRsiSignalSampledRealtimeEvent>(e =>
                    e.Subject.ActorType == ActorType.Realtime
                    && e.EntityId == entityId
                    && e.FuturesPrice == 6425.25m
                    && e.SourceSequence == 9001
                    && e.SourceEventTimestamp == EventTimestamp.UtcDateTime));
            await commandApi.DidNotReceiveWithAnyArgs()
                .GenerateFuturesRsiSignalAsync(default, default);
        }
        finally
        {
            await stopped.ExecuteAsync(context, Status(), Logger());
        }
    }

    [Fact]
    public async Task AtrStarted_UsesHotCachePriceAndFeedTimestamp()
    {
        var entityId = new FuturesAtrSignalEntityId(ContractId, ValueDate, TimeFrameType.TenSeconds, 14);
        var started = new FuturesAtrSignalStartedEvent { EntityId = entityId };
        var stopped = new FuturesAtrSignalStoppedEvent { EntityId = entityId };
        var marketDataApi = CreateMarketDataApi();
        var commandApi = Substitute.For<IActorMarketDataAnalyticsCommandApi>();
        var context = Context();
        var sent = Capture<FuturesAtrSignalSampledRealtimeEvent, FuturesAtrSignalEntityId>(context);

        try
        {
            await started.ExecuteAsync(context, commandApi, marketDataApi, Status(), Logger());
            await sent.Task.WaitAsync(TimeSpan.FromSeconds(1));

            await context.Received(1).SendAsync<FuturesAtrSignalSampledRealtimeEvent, FuturesAtrSignalEntityId>(
                Arg.Is<FuturesAtrSignalSampledRealtimeEvent>(e =>
                    e.Subject.ActorType == ActorType.Realtime
                    && e.EntityId == entityId
                    && e.FuturesPrice == 6425.25m
                    && e.SourceSequence == 9001
                    && e.SourceEventTimestamp == EventTimestamp.UtcDateTime));
            await commandApi.DidNotReceiveWithAnyArgs()
                .GenerateFuturesAtrSignalAsync(default, default);
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
        var context = Context();
        var sent = Capture<FuturesMacdSignalSampledRealtimeEvent, FuturesMacdSignalEntityId>(context);

        try
        {
            await started.ExecuteAsync(context, commandApi, marketDataApi, Status(), Logger());
            await sent.Task.WaitAsync(TimeSpan.FromSeconds(1));

            await context.Received(1).SendAsync<FuturesMacdSignalSampledRealtimeEvent, FuturesMacdSignalEntityId>(
                Arg.Is<FuturesMacdSignalSampledRealtimeEvent>(e =>
                    e.Subject.ActorType == ActorType.Realtime
                    && e.EntityId == entityId
                    && e.FuturesPrice == 6425.25m
                    && e.SourceSequence == 9001
                    && e.SourceEventTimestamp == EventTimestamp.UtcDateTime));
            await commandApi.DidNotReceiveWithAnyArgs()
                .GenerateFuturesMacdSignalAsync(default, default);
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
        var context = Context();
        var sent = Capture<FuturesAdxSignalSampledRealtimeEvent, FuturesAdxSignalEntityId>(context);

        try
        {
            await started.ExecuteAsync(context, commandApi, marketDataApi, Status(), Logger());
            await sent.Task.WaitAsync(TimeSpan.FromSeconds(1));

            await context.Received(1).SendAsync<FuturesAdxSignalSampledRealtimeEvent, FuturesAdxSignalEntityId>(
                Arg.Is<FuturesAdxSignalSampledRealtimeEvent>(e =>
                    e.Subject.ActorType == ActorType.Realtime
                    && e.EntityId == entityId
                    && e.FuturesPrice == 6425.25m
                    && e.SourceSequence == 9001
                    && e.SourceEventTimestamp == EventTimestamp.UtcDateTime));
            await commandApi.DidNotReceiveWithAnyArgs()
                .GenerateFuturesAdxSignalAsync(default, default);
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

    static TaskCompletionSource Capture<TEvent, TEntityId>(IEventActorContext context)
        where TEvent : class, IEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        context.SendAsync<TEvent, TEntityId>(Arg.Do<TEvent>(_ => completion.TrySetResult()))
            .Returns(ValueTask.CompletedTask);
        return completion;
    }

    static IEventActorContext Context() => Substitute.For<IEventActorContext>();
    static IStatusConsoleWriter Status() => Substitute.For<IStatusConsoleWriter>();
    static ILogger Logger() => Substitute.For<ILogger>();
}
