using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Event;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Event;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Event;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event.Actor;
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
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarPublisher;

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
        var context = RsiContext();
        try
        {
            await started.ExecuteAsync(context, context.Logger);
            Assert.Contains(entityId,
                FuturesTradeSessionBarAttachmentRegistry<FuturesRsiSignalEntityId>.Snapshot());
            await context.DidNotReceiveWithAnyArgs()
                .SendAsync<FuturesRsiSignalSampledRealtimeEvent, FuturesRsiSignalEntityId>(default!);
        }
        finally
        {
            await stopped.ExecuteAsync(context, context.Logger);
        }
    }

    [Fact]
    public async Task AtrStarted_AttachesToSharedObservationAndDoesNotSampleHotCache()
    {
        var entityId = new FuturesAtrSignalEntityId(ContractId, ValueDate, TimeFrameType.TenSeconds, 14);
        var started = new FuturesAtrSignalStartedEvent { EntityId = entityId };
        var stopped = new FuturesAtrSignalStoppedEvent { EntityId = entityId };
        var marketDataApi = CreateMarketDataApi();
        var context = AtrContext();
        try
        {
            await started.ExecuteAsync(context, context.Logger);
            Assert.Contains(entityId,
                FuturesTradeSessionBarAttachmentRegistry<FuturesAtrSignalEntityId>.Snapshot());
            await context.DidNotReceiveWithAnyArgs()
                .SendAsync<FuturesAtrSignalSampledRealtimeEvent, FuturesAtrSignalEntityId>(default!);
        }
        finally
        {
            await stopped.ExecuteAsync(context, context.Logger);
        }
    }

    [Fact]
    public async Task MacdStarted_AttachesToSharedObservationAndDoesNotSampleHotCache()
    {
        var entityId = new FuturesMacdSignalEntityId(ContractId, ValueDate, TimeFrameType.TenSeconds, 26);
        var started = new FuturesMacdSignalStartedEvent { EntityId = entityId };
        var stopped = new FuturesMacdSignalStoppedEvent { EntityId = entityId };
        var marketDataApi = CreateMarketDataApi();
        var context = MacdContext();
        try
        {
            await started.ExecuteAsync(context, context.Logger);
            Assert.Contains(entityId,
                FuturesTradeSessionBarAttachmentRegistry<FuturesMacdSignalEntityId>.Snapshot());
            await context.DidNotReceiveWithAnyArgs()
                .SendAsync<FuturesMacdSignalSampledRealtimeEvent, FuturesMacdSignalEntityId>(default!);
        }
        finally
        {
            await stopped.ExecuteAsync(context, context.Logger);
        }
    }

    [Fact]
    public async Task AdxStarted_AttachesToSharedObservationAndDoesNotSampleHotCache()
    {
        var entityId = new FuturesAdxSignalEntityId(ContractId, ValueDate, TimeFrameType.TenSeconds, 14);
        var started = new FuturesAdxSignalStartedEvent { EntityId = entityId };
        var stopped = new FuturesAdxSignalStoppedEvent { EntityId = entityId };
        var context = Substitute.For<IFuturesAdxSignalEventContext>();
        try
        {
            await started.ExecuteAsync(context, Logger());
            Assert.Contains(entityId,
                FuturesTradeSessionBarAttachmentRegistry<FuturesAdxSignalEntityId>.Snapshot());
        }
        finally
        {
            await stopped.ExecuteAsync(context, Logger());
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
    static IFuturesRsiSignalEventContext RsiContext() => Substitute.For<IFuturesRsiSignalEventContext>();
    static IFuturesAtrSignalEventContext AtrContext() => Substitute.For<IFuturesAtrSignalEventContext>();
    static IFuturesMacdSignalEventContext MacdContext() => Substitute.For<IFuturesMacdSignalEventContext>();
    static IStatusConsoleWriter Status() => Substitute.For<IStatusConsoleWriter>();
    static ILogger Logger() => Substitute.For<ILogger>();
}
