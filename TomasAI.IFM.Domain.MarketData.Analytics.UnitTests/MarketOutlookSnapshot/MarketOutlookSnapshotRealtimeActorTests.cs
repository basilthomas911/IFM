using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
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
    public async Task EligibleComponent_ReplacesCacheAndNotifiesImmediately()
    {
        var context = Context();
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

        MarketOutlookHotCache.Shared.TryGetCurrent(id, out var current).Should().BeTrue();
        current.FuturesRsiSignal.Should().Be(rsi);
        current.RefreshTrigger.Should().Be(MarketOutlookRefreshTrigger.Component);
        await context.Received(1).SendAsync<MarketOutlookUpdatedNotifyEvent, MarketOutlookEntityId>(
            Arg.Is<MarketOutlookUpdatedNotifyEvent>(value => value.MarketOutlook == current));
    }

    [Fact]
    public async Task InvalidItiSibling_DoesNotSuppressValidVx()
    {
        var context = Context();
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

        MarketOutlookHotCache.Shared.TryGetCurrent(id, out var current).Should().BeTrue();
        current.VixFuturesPrice.Should().Be(22.25m);
        current.LatestItiTrendSignal.Should().BeNull();
    }

    [Fact]
    public async Task UnsupportedComponentAlone_IsIgnoredWithoutExceptionOrNotification()
    {
        var context = Context();
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
        MarketOutlookHotCache.Shared.TryGetCurrent(id, out _).Should().BeFalse();
        await context.DidNotReceiveWithAnyArgs()
            .SendAsync<MarketOutlookUpdatedNotifyEvent, MarketOutlookEntityId>(default!);
    }

    [Fact]
    public async Task EsTrade_RefreshesEvenWhenDailyAnalyticsAreNotWarm()
    {
        var context = Context();
        var actor = new TestActor(context);
        var trade = WithCurrentTimestamp(
            MarketOutlookDailyPreviewCalculatorTests.Trade("ESZ00", 7_100m, 1));
        var id = new MarketOutlookEntityId(trade.Price.ContractId, trade.Price.ValueDate);
        MarketOutlookHotCache.Shared.Activate(new(id.ContractId, id.ValueDate, Guid.NewGuid()));

        await actor.Receive(context, trade);

        MarketOutlookHotCache.Shared.TryGetCurrent(id, out var current).Should().BeTrue();
        current.RefreshTrigger.Should().Be(MarketOutlookRefreshTrigger.EsTrade);
        current.FuturesEodData.ClosePrice.Should().Be(7_100m);
        current.EsPriceAvailability.Should().Be(MarketOutlookInputAvailability.Available);
        await context.Received(1).SendAsync<MarketOutlookUpdatedNotifyEvent, MarketOutlookEntityId>(
            Arg.Is<MarketOutlookUpdatedNotifyEvent>(value => value.MarketOutlook == current));
    }

    [Fact]
    public async Task DuplicateTrade_IsRejectedButOrdinalGapIsAccepted()
    {
        var context = Context();
        var actor = new TestActor(context);
        var first = MarketOutlookDailyPreviewCalculatorTests.Trade("ESZ00", 7_100m, 1);
        MarketOutlookHotCache.Shared.Activate(new(
            first.Price.ContractId, first.Price.ValueDate, Guid.NewGuid()));
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

        await context.Received(2).SendAsync<MarketOutlookUpdatedNotifyEvent, MarketOutlookEntityId>(
            Arg.Any<MarketOutlookUpdatedNotifyEvent>());
        var id = new MarketOutlookEntityId(gap.Price.ContractId, gap.Price.ValueDate);
        MarketOutlookHotCache.Shared.TryGetCurrent(id, out var current).Should().BeTrue();
        current.FuturesEodData.ClosePrice.Should().Be(7_102m);
    }

    [Fact]
    public async Task EodUpdate_IsAnIndependentRefresh()
    {
        var context = Context();
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
        MarketOutlookHotCache.Shared.Activate(new(id.ContractId, id.ValueDate, Guid.NewGuid()));

        await actor.Receive(context, source);

        MarketOutlookHotCache.Shared.TryGetCurrent(id, out var current).Should().BeTrue();
        current.RefreshTrigger.Should().Be(MarketOutlookRefreshTrigger.EodSession);
        current.FuturesEodData.Should().Be(eod);
    }

    [Fact]
    public async Task NotificationFailure_DoesNotRollbackCacheOrEscapeActorHandler()
    {
        var context = Context();
        context.SendAsync<MarketOutlookUpdatedNotifyEvent, MarketOutlookEntityId>(
                Arg.Any<MarketOutlookUpdatedNotifyEvent>())
            .Returns(_ => ValueTask.FromException(new IOException("injected transport failure")));
        var actor = new TestActor(context);
        var id = Id();

        var action = () => actor.Receive(context, Component(id, 1) with
        {
            VixFuturesPrice = 19.5m
        }).AsTask();

        await action.Should().NotThrowAsync();
        MarketOutlookHotCache.Shared.TryGetCurrent(id, out var current).Should().BeTrue();
        current.VixFuturesPrice.Should().Be(19.5m);
    }

    static MarketOutlookEntityId Id() => new("ESU26", new DateOnly(2026, 9, 1));

    static MarketOutlookComponentChangedRealtimeEvent Component(MarketOutlookEntityId id, long sequence)
    {
        MarketOutlookHotCache.Shared.Activate(new(id.ContractId, id.ValueDate, Guid.NewGuid()));
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

    static IRealtimeActorContext<MarketOutlookSnapshotRealtimeActor> Context()
    {
        var context = Substitute.For<
            IRealtimeActorContext<MarketOutlookSnapshotRealtimeActor>,
            IMarketOutlookSnapshotRealtimeContext>();
        var typed = (IMarketOutlookSnapshotRealtimeContext)context;
        typed.DbFactory.Returns(Substitute.For<IDbContextFactory>());
        typed.Logger.Returns(Substitute.For<ILogger<MarketOutlookSnapshotRealtimeActor>>());
        return context;
    }

    static ActorSubject Subject(string verb, MarketOutlookEntityId id) =>
        new(ActorType.Realtime, MarketOutlookSnapshotRealtimeActor.ActorName, verb, id.Format());
}
