using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesRsiSignal;

public class FuturesRsiSignalStateRepositoryTests
{
    [Fact]
    public async Task LoadStateUsesPeriodLengthAndTypedSnapshotRange()
    {
        const long streamId = 42;
        var command = SampleData.RsiGenerateCommand with
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(
                ActorType.Command,
                GenerateFuturesRsiSignalCommand.Actor,
                GenerateFuturesRsiSignalCommand.Verb,
                SampleData.RsiEntityId.Format())
        };
        var state = new FuturesRsiSignalCommandState();
        var stateFactory = Substitute.For<IEventSourceActorStateFactory>();
        stateFactory.CreateState<FuturesRsiSignalCommandState>().Returns(state);
        var eventDb = Substitute.For<IEventSourceActorDbContext>();
        eventDb.GetEventStreamIdAsync(command.StreamId).Returns(streamId);
        eventDb.MapReduceActorEventStreamFromSnapshotLastNRangeAsync<
                FuturesRsiSignalCommandState,
                FuturesRsiSignalStartedEvent,
                FuturesRsiSignalGeneratedEvent>(
                streamId,
                SampleData.PeriodLength,
                Arg.Any<Action<IEnumerable<EventStreamReadModel>>>())
            .Returns(call =>
            {
                var generatedEvent = new FuturesRsiSignalGeneratedEvent
                {
                    EntityId = SampleData.RsiEntityId,
                    FuturesRsiSignal = SampleData.AtrRsiSignals[0],
                    CreatedOn = SampleData.Timestamp,
                    CreatedBy = "unit-test"
                };
                call.Arg<Action<IEnumerable<EventStreamReadModel>>>()([
                    new EventStreamReadModel
                    {
                        EventVersion = 10,
                        EventTypeName = typeof(FuturesRsiSignalGeneratedEvent).AssemblyQualifiedName!,
                        EventData = JsonConvert.SerializeObject(generatedEvent)
                    }
                ]);
                return ValueTask.CompletedTask;
            });
        var repository = new FuturesRsiSignalStateRepository(
            stateFactory,
            eventDb,
            Substitute.For<IActorService>(),
            Substitute.For<IDbContextFactory>(),
            Substitute.For<IEventProjector<FuturesRsiSignalCommandActor>>(),
            Substitute.For<ILogger<FuturesRsiSignalStateRepository>>());

        var loadedState = await repository.LoadStateAsync(command);

        Assert.Same(state, loadedState);
        Assert.Equal(command.Subject.ThreadId, loadedState.Id);
        Assert.Single(loadedState.FuturesRsiSignals);
        await eventDb.Received(1).MapReduceActorEventStreamFromSnapshotLastNRangeAsync<
            FuturesRsiSignalCommandState,
            FuturesRsiSignalStartedEvent,
            FuturesRsiSignalGeneratedEvent>(
            streamId,
            SampleData.PeriodLength,
            Arg.Any<Action<IEnumerable<EventStreamReadModel>>>());
    }

    [Fact]
    public async Task LoadDailyStateUsesTypedLastNRangeWithoutIntradaySnapshot()
    {
        const long streamId = 84;
        var entityId = new FuturesRsiDailySignalEntityId(
            SampleData.ContractId,
            SampleData.TimePeriod,
            SampleData.PeriodLength);
        var command = new GenerateFuturesRsiDailySignalCommand
        {
            CommandId = Guid.NewGuid(),
            EntityId = entityId,
            Subject = new ActorSubject(
                ActorType.Command,
                GenerateFuturesRsiDailySignalCommand.Actor,
                GenerateFuturesRsiDailySignalCommand.Verb,
                entityId.Format())
        };
        var state = new FuturesRsiSignalCommandState();
        var stateFactory = Substitute.For<IEventSourceActorStateFactory>();
        stateFactory.CreateState<FuturesRsiSignalCommandState>().Returns(state);
        var eventDb = Substitute.For<IEventSourceActorDbContext>();
        eventDb.GetEventStreamIdAsync(command.StreamId).Returns(streamId);
        var repository = new FuturesRsiSignalStateRepository(
            stateFactory,
            eventDb,
            Substitute.For<IActorService>(),
            Substitute.For<IDbContextFactory>(),
            Substitute.For<IEventProjector<FuturesRsiSignalCommandActor>>(),
            Substitute.For<ILogger<FuturesRsiSignalStateRepository>>());

        var loadedState = await repository.LoadStateAsync(command);

        Assert.Same(state, loadedState);
        Assert.Equal(command.Subject.ThreadId, loadedState.Id);
        await eventDb.Received(1).MapReduceActorEventStreamAsync<
            FuturesRsiSignalCommandState,
            FuturesRsiDailySignalGeneratedEvent>(
            streamId,
            entityId.PeriodLength,
            Arg.Any<Action<IEnumerable<EventStreamReadModel>>>());
    }
}
