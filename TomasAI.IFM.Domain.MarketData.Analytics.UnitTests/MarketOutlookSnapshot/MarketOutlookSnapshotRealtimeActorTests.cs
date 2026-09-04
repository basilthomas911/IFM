using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.MarketOutlookSnapshot;

[Collection(MarketOutlookHotCacheTestCollection.Name)]
public sealed class MarketOutlookSnapshotRealtimeActorTests : IDisposable
{
    sealed class TestActor(IRealtimeActorContext<MarketOutlookSnapshotRealtimeActor> context)
        : MarketOutlookSnapshotRealtimeActor(context)
    {
        internal ValueTask Receive(IEventActorContext<MarketOutlookSnapshotRealtimeActor> context, IEvent value) =>
            ReceiveAsync(context, value);
    }

    public MarketOutlookSnapshotRealtimeActorTests() => MarketOutlookHotCache.Shared.Clear();
    public void Dispose() => MarketOutlookHotCache.Shared.Clear();

    [Fact]
    public async Task SnapshotInsertedEvent_IsAnExplicitRealtimeNoOp()
    {
        await using var runtime = await MarketOutlookProcessorTestRuntime.StartAsync();
        var context = Context(runtime.Channel);
        var actor = new TestActor(context);
        var id = Id();
        var inserted = new MarketOutlookSnapshotInsertedEvent
        {
            Subject = Subject(MarketOutlookSnapshotInsertedEvent.Verb, id),
            Id = Guid.NewGuid(),
            CommandId = Guid.NewGuid(),
            EntityId = id,
            EventId = 1,
            ReceivedOn = DateTime.UtcNow,
            MarketOutlook = new() { ContractId = id.ContractId, ValueDate = id.ValueDate }
        };

        var action = () => actor.Receive(context, inserted).AsTask();

        await action.Should().NotThrowAsync();
        runtime.Channel.PendingCount.Should().Be(0);
        await runtime.Publisher.DidNotReceiveWithAnyArgs().PublishAsync(default!, default!, default);
    }

    [Fact]
    public async Task UnmappedEvent_IsIgnoredAsNoOp()
    {
        await using var runtime = await MarketOutlookProcessorTestRuntime.StartAsync();
        var context = Context(runtime.Channel);
        var actor = new TestActor(context);
        var unknown = Substitute.For<IEvent>();

        var action = () => actor.Receive(context, unknown).AsTask();

        await action.Should().NotThrowAsync();
        runtime.Channel.PendingCount.Should().Be(0);
    }

    [Fact]
    public async Task EligibleComponent_ReplacesCacheAndNotifiesImmediately()
    {
        await using var runtime = await MarketOutlookProcessorTestRuntime.StartAsync();
        var context = Context(runtime.Channel);
        var actor = new TestActor(context);
        var id = Id();
        var rsi = SampleData.AtrRsiSignals[0] with
        {
            ContractId = id.ContractId,
            ValueDate = id.ValueDate,
            TimePeriod = TimeFrameType.FifteenSeconds,
            PeriodLength = FuturesIntradaySignalActivationProfile.RsiPeriodLength,
            IsWarm = true
        };

        await actor.Receive(context, Component(id, 1) with { FuturesRsiSignal = rsi });
        await runtime.DrainAsync();

        MarketOutlookHotCache.Shared.TryGetCurrent(id, out var current).Should().BeTrue();
        current.FuturesRsiSignal.Should().Be(rsi);
        current.RefreshTrigger.Should().Be(MarketOutlookRefreshTrigger.Component);
        await runtime.Publisher.DidNotReceiveWithAnyArgs().PublishAsync(
            default!, default!, default);
    }

    [Fact]
    public async Task InvalidItiSibling_DoesNotSuppressValidVx()
    {
        await using var runtime = await MarketOutlookProcessorTestRuntime.StartAsync();
        var context = Context(runtime.Channel);
        var actor = new TestActor(context);
        var id = Id();
        var invalidIti = SampleData.StartOfDayEvent.FuturesItiSignal! with
        {
            ContractId = id.ContractId,
            ValueDate = id.ValueDate,
            TimePeriod = TimeFrameType.Daily,
            IntrinsicTimeMode = IntrinsicTimeModeType.PredictedIntervalChanged
        };

        await actor.Receive(context, Component(id, 1) with
        {
            FuturesItiSignal = invalidIti,
            VixFuturesPrice = 22.25m
        });
        await runtime.DrainAsync();

        MarketOutlookHotCache.Shared.TryGetCurrent(id, out var current).Should().BeTrue();
        current.VixFuturesPrice.Should().Be(22.25m);
        current.FuturesEodData.PriceVolatility.Should().Be(PriceVolatilityType.Flat);
        current.LatestItiTrendSignal.Should().BeNull();
    }

    [Fact]
    public async Task ConsecutiveVxComponents_SeedProvisionalOpenAndThenMove()
    {
        await using var runtime = await MarketOutlookProcessorTestRuntime.StartAsync();
        var context = Context(runtime.Channel);
        var actor = new TestActor(context);
        var id = Id();

        await actor.Receive(context, Component(id, 1) with { VixFuturesPrice = 18m });
        await runtime.DrainAsync();
        MarketOutlookHotCache.Shared.TryGetCurrent(id, out var initial).Should().BeTrue();
        initial.FuturesEodData.PriceVolatility.Should().Be(PriceVolatilityType.Flat);

        await actor.Receive(context, Component(id, 2) with { VixFuturesPrice = 18.25m });
        await runtime.DrainAsync();
        MarketOutlookHotCache.Shared.TryGetCurrent(id, out var moved).Should().BeTrue();
        moved.FuturesEodData.PriceVolatility.Should().Be(PriceVolatilityType.Rising);
    }

    [Fact]
    public async Task DirectVxUpdate_UsesOfficialSessionOpenAndTargetsCurrentEsContract()
    {
        await using var runtime = await MarketOutlookProcessorTestRuntime.StartAsync();
        var marketDataApi = Substitute.For<IMarketDataApi>();
        var valueDate = new DateOnly(2026, 8, 31);
        var vx = Contract("VXU26", "VX", valueDate.AddDays(15));
        var es = Contract("ESU26", "ES", valueDate.AddDays(17));
        marketDataApi.TryGetOnTheRunFuturesContract("VX", out Arg.Any<FuturesContractV3ReadModel>())
            .Returns(call => { call[1] = vx; return true; });
        marketDataApi.TryGetOnTheRunFuturesContract("ES", out Arg.Any<FuturesContractV3ReadModel>())
            .Returns(call => { call[1] = es; return true; });
        marketDataApi.GetFuturesPriceAsync(vx.ContractId).Returns(19m);
        var statistics = new FuturesSessionStatisticsSnapshot(
            vx.ContractId, valueDate, 18m, 19.5m, 17.5m, 42, 1);
        marketDataApi.TryGetFuturesSessionStatistics(
                vx.ContractId, out Arg.Any<FuturesSessionStatisticsSnapshot>())
            .Returns(call => { call[1] = statistics; return true; });
        var context = Context(runtime.Channel, marketDataApi);
        var actor = new TestActor(context);
        var source = WithCurrentTimestamp(
            MarketOutlookDailyPreviewCalculatorTests.Trade(vx.ContractId, 19m, 1));

        await actor.Receive(context, source);
        await runtime.DrainAsync();

        var id = new MarketOutlookEntityId(es.ContractId, valueDate);
        MarketOutlookHotCache.Shared.TryGetCurrent(id, out var current).Should().BeTrue();
        current.VixFuturesPrice.Should().Be(19m);
        current.FuturesEodData.PriceVolatility.Should().Be(PriceVolatilityType.Rising);
    }

    [Fact]
    public async Task OfficialVxStatistics_ReplaceProvisionalOpen()
    {
        await using var runtime = await MarketOutlookProcessorTestRuntime.StartAsync();
        var marketDataApi = Substitute.For<IMarketDataApi>();
        var id = Id();
        var vx = Contract("VXU26", "VX", id.ValueDate.AddDays(15));
        var es = Contract(id.ContractId, "ES", id.ValueDate.AddDays(17));
        marketDataApi.TryGetOnTheRunFuturesContract("VX", out Arg.Any<FuturesContractV3ReadModel>())
            .Returns(call => { call[1] = vx; return true; });
        marketDataApi.TryGetOnTheRunFuturesContract("ES", out Arg.Any<FuturesContractV3ReadModel>())
            .Returns(call => { call[1] = es; return true; });
        marketDataApi.GetFuturesPriceAsync(vx.ContractId).Returns(17m);
        var context = Context(runtime.Channel, marketDataApi);
        var actor = new TestActor(context);
        await actor.Receive(context, Component(id, 1) with { VixFuturesPrice = 19m });
        await runtime.DrainAsync();

        var source = new FuturesSessionStatisticsUpdatedRealtimeEvent
        {
            Subject = new(ActorType.Realtime,
                FuturesSessionStatisticsUpdatedRealtimeEvent.Actor,
                FuturesSessionStatisticsUpdatedRealtimeEvent.Verb,
                $"{vx.ContractId}:{id.ValueDate:yyyyMMdd}"),
            Id = Guid.NewGuid(),
            EntityId = new(vx.ContractId, id.ValueDate),
            CommandId = Guid.NewGuid(),
            ReceivedOn = DateTime.UtcNow,
            Statistics = new(vx.ContractId, id.ValueDate, 18m, 19.5m, 16.5m, 43, 2)
        };
        await actor.Receive(context, source);
        await runtime.DrainAsync();

        MarketOutlookHotCache.Shared.TryGetCurrent(id, out var current).Should().BeTrue();
        current.VixFuturesPrice.Should().Be(17m);
        current.FuturesEodData.PriceVolatility.Should().Be(PriceVolatilityType.Falling);
    }

    [Fact]
    public async Task UnsupportedComponentAlone_IsIgnoredWithoutExceptionOrNotification()
    {
        await using var runtime = await MarketOutlookProcessorTestRuntime.StartAsync();
        var context = Context(runtime.Channel);
        var actor = new TestActor(context);
        var id = Id();
        var invalidIti = SampleData.StartOfDayEvent.FuturesItiSignal! with
        {
            ContractId = id.ContractId,
            ValueDate = id.ValueDate,
            TimePeriod = TimeFrameType.Daily,
            IntrinsicTimeMode = IntrinsicTimeModeType.PredictedIntervalChanged
        };

        var action = () => actor.Receive(context, Component(id, 1) with
        {
            FuturesItiSignal = invalidIti
        }).AsTask();

        await action.Should().NotThrowAsync();
        await runtime.DrainAsync();
        MarketOutlookHotCache.Shared.TryGetCurrent(id, out _).Should().BeFalse();
        await runtime.Publisher.DidNotReceiveWithAnyArgs().PublishAsync(
            default!, default!, default);
    }

    [Fact]
    public async Task EsTrade_RefreshesEvenWhenDailyAnalyticsAreNotWarm()
    {
        await using var runtime = await MarketOutlookProcessorTestRuntime.StartAsync();
        var context = Context(runtime.Channel);
        var actor = new TestActor(context);
        var trade = WithCurrentTimestamp(
            MarketOutlookDailyPreviewCalculatorTests.Trade("ESZ00", 7_100m, 1));
        var id = new MarketOutlookEntityId(trade.Price.ContractId, trade.Price.ValueDate);
        await actor.Receive(context, trade);
        await runtime.DrainAsync();

        MarketOutlookHotCache.Shared.TryGetCurrent(id, out var current).Should().BeTrue();
        current.RefreshTrigger.Should().Be(MarketOutlookRefreshTrigger.EsTrade);
        current.FuturesEodData.ClosePrice.Should().Be(7_100m);
        current.EsPriceAvailability.Should().Be(MarketOutlookInputAvailability.Available);
        await runtime.Publisher.DidNotReceiveWithAnyArgs().PublishAsync(
            default!, default!, default);
    }

    [Fact]
    public async Task RepeatedAndOrdinalGapTrades_AreAllLatestArrivalWrites()
    {
        await using var runtime = await MarketOutlookProcessorTestRuntime.StartAsync();
        var context = Context(runtime.Channel);
        var actor = new TestActor(context);
        var first = MarketOutlookDailyPreviewCalculatorTests.Trade("ESZ00", 7_100m, 1);
        var gap = MarketOutlookDailyPreviewCalculatorTests.Trade("ESZ00", 7_102m, 3) with
        {
            Price = MarketOutlookDailyPreviewCalculatorTests.Trade("ESZ00", 7_102m, 3).Price with
            {
                Trade = MarketOutlookDailyPreviewCalculatorTests.Trade("ESZ00", 7_102m, 3).Price.Trade!.Value with
                {
                    StreamEpochId = first.Price.Trade!.Value.StreamEpochId
                }
            }
        };

        await actor.Receive(context, first);
        await actor.Receive(context, first);
        await actor.Receive(context, gap);
        await runtime.DrainAsync();

        await runtime.Publisher.DidNotReceiveWithAnyArgs().PublishAsync(
            default!, default!, default);
        var id = new MarketOutlookEntityId(gap.Price.ContractId, gap.Price.ValueDate);
        MarketOutlookHotCache.Shared.TryGetCurrent(id, out var current).Should().BeTrue();
        current.FuturesEodData.ClosePrice.Should().Be(7_102m);
    }

    [Fact]
    public async Task EsTrade_DiagnosticLineageCannotSuppressLatestArrivalWrite()
    {
        await using var runtime = await MarketOutlookProcessorTestRuntime.StartAsync();
        var context = Context(runtime.Channel);
        var actor = new TestActor(context);
        var source = MarketOutlookDailyPreviewCalculatorTests.Trade("ESZ00", 7_103m, 1);
        var trade = source.Price.Trade!.Value with
        {
            StreamEpochId = Guid.Empty,
            TradeOrdinal = 0
        };
        source = source with { Price = source.Price with { Trade = trade } };

        await actor.Receive(context, source);
        await runtime.DrainAsync();

        var id = new MarketOutlookEntityId(source.Price.ContractId, source.Price.ValueDate);
        MarketOutlookHotCache.Shared.TryGetCurrent(id, out var current).Should().BeTrue();
        current.FuturesEodData.ClosePrice.Should().Be(7_103m);
        current.RefreshTrigger.Should().Be(MarketOutlookRefreshTrigger.EsTrade);
        await runtime.Publisher.DidNotReceiveWithAnyArgs().PublishAsync(
            default!, default!, default);
    }

    [Fact]
    public async Task EodUpdate_IsAnIndependentRefresh()
    {
        await using var runtime = await MarketOutlookProcessorTestRuntime.StartAsync();
        var context = Context(runtime.Channel);
        var actor = new TestActor(context);
        var id = Id();
        var eod = SampleData.EodData with
        {
            Symbol = "ES",
            ContractId = id.ContractId,
            ValueDate = id.ValueDate
        };
        var source = new MarketOutlookEodUpdatedRealtimeEvent
        {
            Subject = Subject(MarketOutlookEodUpdatedRealtimeEvent.Verb, id),
            Id = Guid.NewGuid(),
            CommandId = Guid.NewGuid(),
            EntityId = id,
            EventId = 1,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test-eod",
            FuturesEodData = eod
        };
        await actor.Receive(context, source);
        await runtime.DrainAsync();

        MarketOutlookHotCache.Shared.TryGetCurrent(id, out var current).Should().BeTrue();
        current.RefreshTrigger.Should().Be(MarketOutlookRefreshTrigger.EodSession);
        current.FuturesEodData.Should().Be(eod);
        await runtime.Publisher.Received(1).PublishAsync(
            Arg.Is<MarketOutlookUpdate>(value => value.Kind == MarketOutlookUpdateKind.Eod),
            Arg.Is<MarketOutlookReadModel>(value => value == current),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotificationFailure_DoesNotRollbackCacheOrEscapeActorHandler()
    {
        await using var runtime = await MarketOutlookProcessorTestRuntime.StartAsync();
        var context = Context(runtime.Channel);
        runtime.Publisher.PublishAsync(
                Arg.Any<MarketOutlookUpdate>(),
                Arg.Any<MarketOutlookReadModel>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromException(new IOException("injected transport failure")));
        var actor = new TestActor(context);
        var id = Id();

        var action = () => actor.Receive(context, Component(id, 1) with
        {
            VixFuturesPrice = 19.5m
        }).AsTask();

        await action.Should().NotThrowAsync();
        await runtime.DrainAsync();
        MarketOutlookHotCache.Shared.TryGetCurrent(id, out var current).Should().BeTrue();
        current.VixFuturesPrice.Should().Be(19.5m);
    }

    static MarketOutlookEntityId Id() => new("ESU26", new DateOnly(2026, 9, 1));

    static MarketOutlookComponentChangedRealtimeEvent Component(MarketOutlookEntityId id, long sequence)
    {
        return new()
        {
            Subject = Subject(MarketOutlookComponentChangedRealtimeEvent.Verb, id),
            Id = Guid.NewGuid(),
            CommandId = Guid.NewGuid(),
            EntityId = id,
            EventId = sequence,
            ReceivedOn = DateTime.UtcNow.AddTicks(sequence),
            EventSource = "unit-test"
        };
    }

    static TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events.FuturesMarketPriceUpdatedRealtimeEvent
        WithCurrentTimestamp(
            TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events.FuturesMarketPriceUpdatedRealtimeEvent source) =>
        source with
        {
            Price = source.Price with
            {
                Trade = source.Price.Trade!.Value with { EventTimestamp = DateTimeOffset.UtcNow }
            }
        };

    static IRealtimeActorContext<MarketOutlookSnapshotRealtimeActor> Context(
        IMarketOutlookUpdateWriter writer,
        IMarketDataApi? marketDataApi = null)
    {
        var context = Substitute.For<
            IRealtimeActorContext<MarketOutlookSnapshotRealtimeActor>,
            IMarketOutlookSnapshotRealtimeContext>();
        var typed = (IMarketOutlookSnapshotRealtimeContext)context;
        typed.DbFactory.Returns(Substitute.For<IDbContextFactory>());
        typed.Logger.Returns(Substitute.For<ILogger<MarketOutlookSnapshotRealtimeActor>>());
        typed.UpdateWriter.Returns(writer);
        typed.MarketDataApi.Returns(marketDataApi ?? Substitute.For<IMarketDataApi>());
        return context;
    }

    static FuturesContractV3ReadModel Contract(
        string contractId,
        string symbol,
        DateOnly lastTradeDate) => new(
        contractId,
        contractId,
        symbol,
        contractId,
        "FUT",
        "USD",
        "CME",
        "1",
        lastTradeDate,
        true);

    static ActorSubject Subject(string verb, MarketOutlookEntityId id) =>
        new(ActorType.Realtime, MarketOutlookSnapshotRealtimeActor.ActorName, verb, id.Format());
}
