using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesTdiSignal;

public sealed class FuturesTdiSignalRealtimeActorTests
{
    [Fact]
    public void ParseMessage_RoutedRsiWindowWithEmptyCommandId_ReturnsConcreteEvent()
    {
        var @event = RsiWindow(Guid.Empty);
        var message = Substitute.For<IActorMessage>();
        message.Subject.Returns(@event.Subject);
        message.AsEvent<FuturesRsiSignalsGeneratedEvent>().Returns(@event);
        var actor = CreateActor(out _);

        var parsed = actor.Parse(
            Substitute.For<IEventActorContext<FuturesTdiSignalRealtimeActor>>(),
            message);

        parsed.Should().BeSameAs(@event);
    }

    [Fact]
    public async Task ReceiveAsync_EligibleRsiWindow_ProjectsOneRealtimeTdiEvent()
    {
        var actor = CreateActor(out var projector);
        projector.ProcessRealtimeEventAsync(Arg.Any<IEvent>())
            .Returns(ValueTask.FromResult(true));

        await actor.Receive(
            Substitute.For<IEventActorContext<FuturesTdiSignalRealtimeActor>>(),
            RsiWindow(Guid.NewGuid()));

        await projector.Received(1).ProcessRealtimeEventAsync(
            Arg.Is<FuturesTdiSignalGeneratedEvent>(generated =>
                generated.Subject.ActorType == ActorType.Realtime
                && generated.Subject.Name == FuturesTdiSignalRealtimeActor.ActorName
                && generated.FuturesTdiSignal != null));
    }

    [Fact]
    public async Task Lifecycle_RegistersRsiRouteAndControlsProjector()
    {
        var actor = CreateActor(out var projector);
        var context = Substitute.For<IEventActorContext<FuturesTdiSignalRealtimeActor>>();
        var route = new ActorTypeId(
            ActorType.Realtime,
            FuturesRsiSignalRealtimeActor.ActorName,
            FuturesRsiSignalsGeneratedEvent.Verb);

        await actor.Start(context);
        await actor.Stop(context);

        context.Received(1).AddRealtimeRouter(route, actor.Id);
        context.Received(1).RemoveRealtimeRouter(route, actor.Id);
        await projector.Received(1).StartAsync(context, Arg.Any<CancellationToken>());
        await projector.Received(1).StopAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RestartSeed_LoadsPriorTdiExactlyOncePerIdentity()
    {
        var state = new FuturesTdiSignalRealtimeState();
        var db = Substitute.For<IMarketDataDbReadContext>();
        var source = RsiWindow(Guid.NewGuid());

        await state.SeedAsync(source, FuturesTdiConfiguration.Standard, db);
        await state.SeedAsync(source, FuturesTdiConfiguration.Standard, db);

        await db.Received(1).GetLastFuturesTdiSignalAsync(
            source.EntityId.ContractId,
            source.EntityId.ValueDate,
            source.EntityId.TimePeriod,
            FuturesTdiConfiguration.StandardConfigurationId);
    }

    static TestableFuturesTdiSignalRealtimeActor CreateActor(
        out IRealtimeProjector<FuturesTdiSignalRealtimeActor> projector)
    {
        var supervisor = Substitute.For<IActorSupervisor>();
        projector = Substitute.For<IRealtimeProjector<FuturesTdiSignalRealtimeActor>>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        var marketDataDb = Substitute.For<IMarketDataDbContext>();
        dbFactory.MarketDataDb.Returns(marketDataDb);
        var logger = Substitute.For<ILogger<FuturesTdiSignalRealtimeActor>>();
        return new TestableFuturesTdiSignalRealtimeActor(
            new FuturesTdiSignalRealtimeContext(supervisor, projector, dbFactory, logger));
    }

    static FuturesRsiSignalsGeneratedEvent RsiWindow(Guid commandId)
    {
        var entityId = new FuturesRsiSignalEntityId(
            SampleData.ContractId,
            SampleData.ValueDate,
            TimeFrameType.OneMinute,
            FuturesTdiConfiguration.Standard.RsiPeriod);
        return new FuturesRsiSignalsGeneratedEvent
        {
            Subject = new ActorSubject(
                ActorType.Realtime,
                FuturesTdiSignalRealtimeActor.ActorName,
                FuturesRsiSignalsGeneratedEvent.Verb,
                entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId,
            EntityId = entityId,
            AggregateId = entityId.Format(),
            EventSource = "unit-test",
            ReceivedOn = DateTime.UtcNow,
            PeriodLength = FuturesTdiConfiguration.Standard.RsiPeriod,
            FuturesRsiSignals = SampleData.TdiRsiSignals
                .Select(static signal => signal with { IsWarm = true })
                .ToArray()
        };
    }

    sealed class TestableFuturesTdiSignalRealtimeActor(
        IRealtimeActorContext<FuturesTdiSignalRealtimeActor> context)
        : FuturesTdiSignalRealtimeActor(context)
    {
        public IEvent Parse(
            IEventActorContext<FuturesTdiSignalRealtimeActor> context,
            IActorMessage message) => ParseMessage(context, message);

        public ValueTask Receive(
            IEventActorContext<FuturesTdiSignalRealtimeActor> context,
            IEvent @event) => ReceiveAsync(context, @event);

        public ValueTask Start(IEventActorContext<FuturesTdiSignalRealtimeActor> context) =>
            OnStartup(context);

        public ValueTask Stop(IEventActorContext<FuturesTdiSignalRealtimeActor> context) =>
            OnShutdown(context);
    }
}
