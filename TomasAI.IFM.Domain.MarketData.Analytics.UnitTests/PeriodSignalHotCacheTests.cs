using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Event;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Event;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Event;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Observation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests;

public sealed class PeriodSignalHotCacheTests
{
    const string ContractId = "ESU26-HOT-CACHE";
    static readonly DateOnly ValueDate = new(2026, 8, 14);
    static readonly DateTimeOffset EventTimestamp = new(2026, 8, 14, 15, 30, 45, TimeSpan.Zero);

    [Fact]
    public async Task RsiStarted_AttachesToSharedObservationAndDoesNotSampleHotCache()
    {
        var entityId = new FuturesRsiSignalEntityId(ContractId, ValueDate, TimeFrameType.TenSeconds, 13);
        var started = new FuturesRsiSignalStartedEvent { EntityId = entityId };
        var stopped = new FuturesRsiSignalStoppedEvent { EntityId = entityId };
        var marketDataApi = CreateMarketDataApi();
        var commandApi = Substitute.For<IEventActorContext>();
        var context = Context();
        try
        {
            await started.ExecuteAsync(context, commandApi, marketDataApi, Status(), Logger());
            Assert.Contains(entityId,
                FuturesAnalyticsObservationAttachmentRegistry<FuturesRsiSignalEntityId>.Snapshot());
            await context.DidNotReceiveWithAnyArgs()
                .SendAsync<FuturesRsiSignalSampledRealtimeEvent, FuturesRsiSignalEntityId>(default!);
            await commandApi.DidNotReceiveWithAnyArgs()
                .RequestAsync<GenerateFuturesRsiSignalCommand, FuturesRsiSignalEntityId>(default!);
        }
        finally
        {
            await stopped.ExecuteAsync(context, Status(), Logger());
        }
    }

    [Fact]
    public async Task AtrStarted_AttachesToSharedObservationAndDoesNotSampleHotCache()
    {
        var entityId = new FuturesAtrSignalEntityId(ContractId, ValueDate, TimeFrameType.TenSeconds, 14);
        var started = new FuturesAtrSignalStartedEvent { EntityId = entityId };
        var stopped = new FuturesAtrSignalStoppedEvent { EntityId = entityId };
        var marketDataApi = CreateMarketDataApi();
        var commandApi = Substitute.For<IEventActorContext>();
        var context = Context();
        try
        {
            await started.ExecuteAsync(context, commandApi, marketDataApi, Status(), Logger());
            Assert.Contains(entityId,
                FuturesAnalyticsObservationAttachmentRegistry<FuturesAtrSignalEntityId>.Snapshot());
            await context.DidNotReceiveWithAnyArgs()
                .SendAsync<FuturesAtrSignalSampledRealtimeEvent, FuturesAtrSignalEntityId>(default!);
            await commandApi.DidNotReceiveWithAnyArgs()
                .RequestAsync<GenerateFuturesAtrSignalCommand, FuturesAtrSignalEntityId>(default!);
        }
        finally
        {
            await stopped.ExecuteAsync(Context(), Status(), Logger());
        }
    }

    [Fact]
    public async Task MacdStarted_AttachesToSharedObservationAndDoesNotSampleHotCache()
    {
        var entityId = new FuturesMacdSignalEntityId(ContractId, ValueDate, TimeFrameType.TenSeconds, 26);
        var started = new FuturesMacdSignalStartedEvent { EntityId = entityId };
        var stopped = new FuturesMacdSignalStoppedEvent { EntityId = entityId };
        var marketDataApi = CreateMarketDataApi();
        var commandApi = Substitute.For<IEventActorContext>();
        var context = Context();
        try
        {
            await started.ExecuteAsync(context, commandApi, marketDataApi, Status(), Logger());
            Assert.Contains(entityId,
                FuturesAnalyticsObservationAttachmentRegistry<FuturesMacdSignalEntityId>.Snapshot());
            await context.DidNotReceiveWithAnyArgs()
                .SendAsync<FuturesMacdSignalSampledRealtimeEvent, FuturesMacdSignalEntityId>(default!);
            await commandApi.DidNotReceiveWithAnyArgs()
                .RequestAsync<GenerateFuturesMacdSignalCommand, FuturesMacdSignalEntityId>(default!);
        }
        finally
        {
            await stopped.ExecuteAsync(Context(), Status(), Logger());
        }
    }

    [Fact]
    public async Task AdxStarted_AttachesToSharedObservationAndDoesNotSampleHotCache()
    {
        var entityId = new FuturesAdxSignalEntityId(ContractId, ValueDate, TimeFrameType.TenSeconds, 14);
        var started = new FuturesAdxSignalStartedEvent { EntityId = entityId };
        var stopped = new FuturesAdxSignalStoppedEvent { EntityId = entityId };
        var marketDataApi = CreateMarketDataApi();
        var commandApi = Substitute.For<IEventActorContext>();
        var context = Context();
        try
        {
            await started.ExecuteAsync(context, commandApi, marketDataApi, Status(), Logger());
            Assert.Contains(entityId,
                FuturesAnalyticsObservationAttachmentRegistry<FuturesAdxSignalEntityId>.Snapshot());
            await context.DidNotReceiveWithAnyArgs()
                .SendAsync<FuturesAdxSignalSampledRealtimeEvent, FuturesAdxSignalEntityId>(default!);
            await commandApi.DidNotReceiveWithAnyArgs()
                .RequestAsync<GenerateFuturesAdxSignalCommand, FuturesAdxSignalEntityId>(default!);
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

    static IEventActorContext Context() => Substitute.For<IEventActorContext>();
    static IStatusConsoleWriter Status() => Substitute.For<IStatusConsoleWriter>();
    static ILogger Logger() => Substitute.For<ILogger>();
}
