using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesBbSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesBbSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.HistoricalDataLoader;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests;

/// <summary>Protects analytics states whose latest domain event is a complete replay snapshot.</summary>
public sealed class SnapshotStateRepositoryTests
{
    /// <summary>Ensures checkpoint-based analytics repositories request only their latest snapshot event.</summary>
    [Fact]
    public async Task CheckpointRepositoriesUseDirectSnapshotLoading()
    {
        const long streamId = 91;
        var stateFactory = Substitute.For<IEventSourceActorStateFactory>();
        stateFactory.CreateState<FuturesBbSignalCommandState>().Returns(new FuturesBbSignalCommandState());
        stateFactory.CreateState<FuturesEmaSignalCommandState>().Returns(new FuturesEmaSignalCommandState());
        stateFactory.CreateState<FuturesVwapSignalCommandState>().Returns(new FuturesVwapSignalCommandState());
        stateFactory.CreateState<FuturesVxTermStructureSignalCommandState>()
            .Returns(new FuturesVxTermStructureSignalCommandState());
        var eventSource = Substitute.For<IEventSourceActorDbContext>();
        eventSource.GetEventStreamIdAsync(Arg.Any<string>()).Returns(streamId);
        var actorService = Substitute.For<IActorService>();
        var command = Command();

        await new FuturesBbSignalStateRepository(
            stateFactory,
            eventSource,
            actorService,
            Substitute.For<IEventProjector<FuturesBbSignalCommandActor>>(),
            Substitute.For<ILogger<FuturesBbSignalStateRepository>>()).LoadStateAsync(command);
        await new FuturesEmaSignalStateRepository(
            stateFactory,
            eventSource,
            actorService,
            Substitute.For<IEventProjector<FuturesEmaSignalCommandActor>>(),
            Substitute.For<ILogger<FuturesEmaSignalStateRepository>>()).LoadStateAsync(command);
        await new FuturesVwapSignalStateRepository(
            stateFactory,
            eventSource,
            actorService,
            Substitute.For<IEventProjector<FuturesVwapSignalCommandActor>>(),
            Substitute.For<ILogger<FuturesVwapSignalStateRepository>>()).LoadStateAsync(command);
        await new FuturesVxTermStructureSignalStateRepository(
            stateFactory,
            eventSource,
            actorService,
            Substitute.For<IEventProjector<FuturesVxTermStructureSignalCommandActor>>(),
            Substitute.For<ILogger<FuturesVxTermStructureSignalStateRepository>>()).LoadStateAsync(command);

        await ReceivedSnapshot<FuturesBbSignalCommandState, FuturesBbSignalGeneratedEvent>(eventSource, streamId);
        await ReceivedSnapshot<FuturesEmaSignalCommandState, FuturesEmaSignalGeneratedEvent>(eventSource, streamId);
        await ReceivedSnapshot<FuturesVwapSignalCommandState, FuturesVwapSignalUpdatedEvent>(eventSource, streamId);
        await ReceivedSnapshot<FuturesVxTermStructureSignalCommandState, FuturesVxTermStructureSignalUpdatedEvent>(
            eventSource,
            streamId);
        await eventSource.DidNotReceiveWithAnyArgs()
            .MapReduceActorEventStreamFromSnapshotLastNRangeAsync<
                FuturesBbSignalCommandState,
                FuturesBbSignalGeneratedEvent,
                FuturesBbSignalGeneratedEvent>(default, default, default!);
    }

    /// <summary>Ensures one historical-load attempt begins replay at its accepted-request event.</summary>
    [Fact]
    public async Task HistoricalDataLoaderUsesRequestedEventSnapshot()
    {
        const long streamId = 92;
        var stateFactory = Substitute.For<IEventSourceActorStateFactory>();
        stateFactory.CreateState<FuturesAnalyticsHistoricalDataLoaderCommandState>()
            .Returns(new FuturesAnalyticsHistoricalDataLoaderCommandState());
        var eventSource = Substitute.For<IEventSourceActorDbContext>();
        eventSource.GetEventStreamIdAsync(Arg.Any<string>()).Returns(streamId);
        var repository = new FuturesAnalyticsHistoricalDataLoaderStateRepository(
            stateFactory,
            eventSource,
            Substitute.For<IActorService>(),
            Substitute.For<IEventProjector<FuturesAnalyticsHistoricalDataLoaderCommandActor>>(),
            Substitute.For<ILogger<FuturesAnalyticsHistoricalDataLoaderStateRepository>>());

        await repository.LoadStateAsync(Command());

        await ReceivedSnapshot<
            FuturesAnalyticsHistoricalDataLoaderCommandState,
            FuturesAnalyticsHistoricalDataLoaderRequestedEvent>(eventSource, streamId);
        await eventSource.DidNotReceiveWithAnyArgs()
            .MapReduceActorEventStreamAsync<FuturesAnalyticsHistoricalDataLoaderCommandState>(default, default!);
    }

    static ICommand Command()
    {
        var command = Substitute.For<ICommand>();
        command.CommandName.Returns("SnapshotLoad");
        command.StreamId.Returns("snapshot-load-stream");
        command.Subject.Returns(new ActorSubject(ActorType.Command, "SnapshotLoad", "Load", "snapshot"));
        return command;
    }

    static async ValueTask ReceivedSnapshot<TState, TEvent>(
        IEventSourceActorDbContext eventSource,
        long streamId)
        where TState : IActorState<TState>
        where TEvent : IEvent
        => await eventSource.Received(1).MapReduceActorEventStreamAsync<TState, TEvent>(
            streamId,
            Arg.Any<Action<IEnumerable<EventStreamReadModel>>>());
}
