using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.State;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.EventProjector;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.UnitTests.TradeFlow;

public sealed class IronCondorPositionLivePublicationTests
{
    [Fact]
    public async Task Committed_market_mark_reaches_ui_and_plan_before_blocked_history_admission()
    {
        var fixture = new Fixture();
        var history = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var admitted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Projector.DomainEventsProjectionAsync(Arg.Any<DomainEventCollection>()).Returns(_ =>
        { admitted.TrySetResult(); return new ValueTask(history.Task); });
        var pending = fixture.Repository.SaveResidentEventsAsync(fixture.Context, fixture.Events,
            fixture.Command, 7, default).AsTask();
        await admitted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(pending.IsCompleted);
        var published = fixture.Context.ReceivedCalls().Select(call => (IronCondorPositionChangedEvent)call.GetArguments()[0]!).ToArray();
        Assert.Equal(new[] { ActorType.Event, ActorType.Realtime }, published.Select(value => value.Subject.ActorType));
        Assert.All(published, value => { Assert.Equal(123, value.EventId); Assert.Equal(fixture.Command.CommandId, value.CommandId); });
        history.SetResult(); await pending;
    }

    [Fact]
    public async Task Failed_source_commit_never_publishes_a_position_or_admits_history()
    {
        var fixture = new Fixture();
        fixture.Database.SaveCommandEventsAtomicallyAsync(fixture.Command, fixture.Events, 7, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<DomainEventCollection>(new InvalidOperationException("source unavailable")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Repository.SaveResidentEventsAsync(
            fixture.Context, fixture.Events, fixture.Command, 7, default).AsTask());
        Assert.Empty(fixture.Context.ReceivedCalls()); Assert.Empty(fixture.Projector.ReceivedCalls());
    }

    [Theory]
    [InlineData(StrategyPositionPhase.Open)]
    [InlineData(StrategyPositionPhase.Close)]
    [InlineData(StrategyPositionPhase.Correction)]
    public async Task Financial_boundaries_keep_the_existing_projection_publication_path(StrategyPositionPhase phase)
    {
        var fixture = new Fixture();
        var changed = (IronCondorPositionChangedEvent)fixture.Events[0];
        fixture.Events[0] = changed with { PositionSnapshot = changed.PositionSnapshot with { Phase = phase } };
        await fixture.Repository.SaveResidentEventsAsync(fixture.Context, fixture.Events, fixture.Command, 7, default);
        Assert.Empty(fixture.Context.ReceivedCalls());
        await fixture.Projector.Received(1).DomainEventsProjectionAsync(Arg.Any<DomainEventCollection>());
    }

    [Fact]
    public async Task A_failed_ui_publication_does_not_prevent_the_independent_plan_notification()
    {
        var fixture = new Fixture();
        fixture.Context.SendAsync<IronCondorPositionChangedEvent, StrategyPositionId>(Arg.Any<IronCondorPositionChangedEvent>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<IronCondorPositionChangedEvent>(0).Subject.ActorType == ActorType.Event
                ? ValueTask.FromException(new IOException("UI publication unavailable")) : ValueTask.CompletedTask);
        await fixture.Repository.SaveResidentEventsAsync(fixture.Context, fixture.Events, fixture.Command, 7, default);
        await fixture.Context.Received(1).SendAsync<IronCondorPositionChangedEvent, StrategyPositionId>(
            Arg.Is<IronCondorPositionChangedEvent>(value => value.Subject.ActorType == ActorType.Realtime && value.EventId == 123), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(StrategyPositionPhase.MarkToMarket, false)]
    [InlineData(StrategyPositionPhase.Open, true)]
    public async Task Replayed_market_history_never_regenerates_live_plans_but_financial_boundaries_are_published(
        StrategyPositionPhase phase, bool publishesBoundary)
    {
        var context = Substitute.For<IIronCondorPositionCommandContext>();
        var projector = new IronCondorPositionEventProjector(context);
        var descriptor = projector.ProjectionDescriptors.Single(value => value.SourceEventType == typeof(IronCondorPositionChangedEvent));
        Assert.False(descriptor.PublishProcessingEvent);
        Assert.True(descriptor.UseDurableReplay); // Existing financial/position history recovery is preserved.
        var fixture = new Fixture(); var changed = (IronCondorPositionChangedEvent)fixture.Events[0];
        changed = changed with { PositionSnapshot = changed.PositionSnapshot with { Phase = phase } };
        await descriptor.ApplyAsync(changed, default!);
        await context.DbFactory.TradeDb.Received(1).UpsertStrategyPositionAsync(changed.PositionSnapshot, Arg.Any<CancellationToken>());
        var sent = context.ReceivedCalls().Where(call => call.GetMethodInfo().Name == "SendAsync").ToArray();
        Assert.Equal(publishesBoundary, sent.Length > 0);
    }

    [Fact]
    public void Market_mark_event_identity_is_created_before_persistence_and_is_stable_on_retry()
    {
        var id = StrategyPositionId.Create(new(101,701,1701,1101), TradeStrategyKind.IronCondor);
        var snapshot = new StrategyPositionSnapshot { Id = id };
        var command = new ChangeTradeLegDataCommand { CommandId = Guid.NewGuid(), EntityId = id };
        var first = command.CreateIronCondorPositionChangedEvent(snapshot);
        var retry = command.CreateIronCondorPositionChangedEvent(snapshot);
        Assert.NotEqual(Guid.Empty, first.Id); Assert.Equal(command.CommandId, first.CommandId);
        Assert.Equal(first.Id, retry.Id);
        Assert.NotEqual(first.Id, (command with { CommandId = Guid.NewGuid() }).CreateIronCondorPositionChangedEvent(snapshot).Id);
        var alternate = new UpdateIronCondorPositionLegMarketPriceCommand { CommandId = Guid.NewGuid(), EntityId = id };
        Assert.NotEqual(Guid.Empty, alternate.CreateIronCondorPositionChangedEvent(snapshot).Id);
        Assert.Equal(alternate.CommandId, alternate.CreateIronCondorPositionChangedEvent(snapshot).CommandId);
    }

    sealed class Fixture
    {
        public IEventSourceActorDbContext Database { get; } = Substitute.For<IEventSourceActorDbContext>();
        public IEventProjector<FuturesIronCondorTradePositionCommandActor> Projector { get; } = Substitute.For<IEventProjector<FuturesIronCondorTradePositionCommandActor>>();
        public ICommandActorContext Context { get; } = Substitute.For<ICommandActorContext>();
        public ChangeTradeLegDataCommand Command { get; } = new() { CommandId = Guid.NewGuid() };
        public DomainEventCollection Events { get; }
        public IronCondorPositionStateRepository Repository { get; }
        public Fixture()
        {
            var id = StrategyPositionId.Create(new(101,701,1701,1101), TradeStrategyKind.IronCondor);
            Events = new([new IronCondorPositionChangedEvent { Id = Guid.NewGuid(), CommandId = Command.CommandId,
                EntityId = id, PositionSnapshot = new() { Id = id, PositionSequence = 8, Phase = StrategyPositionPhase.MarkToMarket } }]);
            Database.SaveCommandEventsAtomicallyAsync(Command, Events, 7, Arg.Any<CancellationToken>())
                .Returns(_ => Task.FromResult(new DomainEventCollection([((IronCondorPositionChangedEvent)Events[0]) with { EventId = 123 }])));
            Repository = new(Substitute.For<IEventSourceActorStateFactory>(), Database,
                Substitute.For<IActorService>(), Projector, NullLogger<IronCondorPositionStateRepository>.Instance);
        }
    }
}
