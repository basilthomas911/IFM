using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.State;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Domain.Trade.UnitTests.TradeFlow;

public sealed class IronCondorPositionReloadTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Reload_preserves_committed_stream_version_for_the_first_live_update(bool cancellable, bool limitsAfterSnapshot)
    {
        var state = new IronCondorPositionCommandState();
        var factory = Substitute.For<IEventSourceActorStateFactory>(); factory.CreateState<IronCondorPositionCommandState>().Returns(state);
        var db = Substitute.For<IEventSourceActorDbContext>();
        db.ProviderName.Returns("System.Data.Postgres");
        db.GetCommittedStreamVersionAsync(Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(11L);
        var id = StrategyPositionId.Create(new(101,701,1701,1101), TradeStrategyKind.IronCondor);
        var snapshot = new StrategyPositionSnapshot { Id = id, PositionSequence = 9, RouteGeneration = 1 };
        var source = new IronCondorPositionChangedEvent { EntityId = id, PositionSnapshot = snapshot };
        var rows = new[] { new EventStreamReadModel { StreamVersion = 7, EventVersion = 103,
            EventTypeName = typeof(IronCondorPositionChangedEvent).AssemblyQualifiedName!, EventData = EventLogMessagePackCodec.Shared.Serialize(source) } };
        var limits = new IronCondorMonitoringInitializedEvent { EntityId = id,
            TradeLimits = new() { MaxLossLimit = 1000 }, SpreadLimits = [new() { }] };
        var limitRows = new[] { new EventStreamReadModel { StreamVersion = limitsAfterSnapshot ? 11 : 2, EventVersion = limitsAfterSnapshot ? 107 : 98,
            EventTypeName = typeof(IronCondorMonitoringInitializedEvent).AssemblyQualifiedName!, EventData = EventLogMessagePackCodec.Shared.Serialize(limits) } };
        db.MapReduceActorEventStreamAsync<IronCondorPositionCommandState, IronCondorPositionChangedEvent>(Arg.Any<long>(), Arg.Any<Action<IEnumerable<EventStreamReadModel>>>(), Arg.Any<CancellationToken>())
            .Returns(call => { call.ArgAt<Action<IEnumerable<EventStreamReadModel>>>(1)(limitsAfterSnapshot ? rows.Concat(limitRows) : rows); return ValueTask.CompletedTask; });
        db.MapReduceActorEventStreamAsync<IronCondorPositionCommandState, IronCondorMonitoringInitializedEvent>(Arg.Any<long>(), 1, Arg.Any<Action<IEnumerable<EventStreamReadModel>>>(), Arg.Any<CancellationToken>())
            .Returns(call => { call.ArgAt<Action<IEnumerable<EventStreamReadModel>>>(2)(limitRows); return ValueTask.CompletedTask; });
        var projector = Substitute.For<IEventProjector<FuturesIronCondorTradePositionCommandActor>>();
        var repository = new IronCondorPositionStateRepository(factory, db, Substitute.For<IActorService>(), projector,
            Substitute.For<ILogger<IronCondorPositionStateRepository>>());
        var command = new ChangeTradeLegDataCommand { CommandId = Guid.NewGuid(), EntityId = id,
            Subject = new(ActorType.Command, FuturesIronCondorTradePositionCommandActor.ActorName, ChangeTradeLegDataCommand.Verb, id.Format()) };
        using var cancellation = new CancellationTokenSource();
        var loaded = await repository.LoadStateAsync(command, cancellable ? cancellation.Token : default);
        Assert.Equal(11, loaded.CommittedStreamVersion);
        Assert.Equal(9, loaded.PositionSnapshot!.PositionSequence);
        Assert.Equal(1000, loaded.TradeLimits!.MaxLossLimit);
        Assert.Single(loaded.SpreadLimits);
        Assert.Empty(loaded.Events);
        await db.Received(1).MapReduceActorEventStreamAsync<IronCondorPositionCommandState, IronCondorPositionChangedEvent>(Arg.Any<long>(), Arg.Any<Action<IEnumerable<EventStreamReadModel>>>(), cancellable ? cancellation.Token : default);
        await db.Received(limitsAfterSnapshot ? 0 : 1).MapReduceActorEventStreamAsync<IronCondorPositionCommandState, IronCondorMonitoringInitializedEvent>(Arg.Any<long>(), 1, Arg.Any<Action<IEnumerable<EventStreamReadModel>>>(), Arg.Any<CancellationToken>());
        await db.DidNotReceive().MapReduceActorEventStreamAsync<IronCondorPositionCommandState>(Arg.Any<long>(), Arg.Any<Action<IEnumerable<EventStreamReadModel>>>(), Arg.Any<CancellationToken>());
        loaded.Apply(source with { CommandId = command.CommandId, PositionSnapshot = snapshot with { PositionSequence = 10 } });
        db.SaveCommandEventsAtomicallyAsync(command, Arg.Any<DomainEventCollection>(), 11, Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(call.ArgAt<DomainEventCollection>(1)));
        await repository.SaveResidentEventsAsync(Substitute.For<ICommandActorContext>(), loaded.DetachChanges(), command, loaded.CommittedStreamVersion, default);
        await db.Received(1).SaveCommandEventsAtomicallyAsync(command, Arg.Any<DomainEventCollection>(), 11, Arg.Any<CancellationToken>());
    }
}
