using FluentAssertions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesRsiSignal;

public class FuturesRsiSignalStateRepositoryTests
{
    [Fact]
    public async Task Missing_generated_signal_window_preserves_the_committed_restart_marker_version()
    {
        var command = SampleData.RsiGenerateCommand;
        var state = new FuturesRsiSignalCommandState();
        var factory = Substitute.For<IEventSourceActorStateFactory>();
        factory.CreateState<FuturesRsiSignalCommandState>().Returns(state);
        var eventDb = Substitute.For<IEventSourceActorDbContext>();
        eventDb.ProviderName.Returns("System.Data.Postgres");
        eventDb.GetEventStreamIdAsync(command.StreamId, CancellationToken.None).Returns(42L);
        eventDb.GetCommittedStreamVersionAsync(42L, CancellationToken.None).Returns(3L);
        var repository = new FuturesRsiSignalStateRepository(factory, eventDb, Substitute.For<IActorService>(),
            Substitute.For<IDbContextFactory>(), Substitute.For<IEventProjector<FuturesRsiSignalCommandActor>>(),
            Substitute.For<ILogger<FuturesRsiSignalStateRepository>>());
        var loaded = await repository.LoadStateAsync(command);
        loaded.CommittedStreamVersion.Should().Be(3);
        loaded.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadStateUsesPeriodLengthAndTypedRangeAcrossRestartMarkers()
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
        eventDb.GetEventStreamIdAsync(command.StreamId, CancellationToken.None).Returns(streamId);
        eventDb.MapReduceActorEventStreamAsync<
                FuturesRsiSignalCommandState,
                FuturesRsiSignalGeneratedEvent>(
                streamId,
                SampleData.PeriodLength,
                Arg.Any<Action<IEnumerable<EventStreamReadModel>>>(), CancellationToken.None)
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
                        EventData = TomasAI.IFM.Shared.EventSourcing.EventLogMessagePackCodec.Shared.Serialize(generatedEvent)
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
        await eventDb.Received(1).MapReduceActorEventStreamAsync<
            FuturesRsiSignalCommandState,
            FuturesRsiSignalGeneratedEvent>(
            streamId,
            SampleData.PeriodLength,
            Arg.Any<Action<IEnumerable<EventStreamReadModel>>>(), CancellationToken.None);
        await eventDb.DidNotReceiveWithAnyArgs().MapReduceActorEventStreamFromSnapshotLastNRangeAsync<
            FuturesRsiSignalCommandState,
            FuturesRsiSignalStartedEvent,
            FuturesRsiSignalGeneratedEvent>(default, default, default!, CancellationToken.None);
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
        eventDb.GetEventStreamIdAsync(command.StreamId, CancellationToken.None).Returns(streamId);
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
            Arg.Any<Action<IEnumerable<EventStreamReadModel>>>(), CancellationToken.None);
    }

    [Fact]
    public async Task LoadIntradayTdiStateRestoresSeedAndCompleteWarmWindowAcrossRestart()
    {
        const long streamId = 126;
        var configuration = FuturesTdiConfiguration.Standard;
        var entityId = new FuturesRsiSignalEntityId(
            SampleData.ContractId,
            SampleData.ValueDate,
            TimeFrameType.FifteenSeconds,
            configuration.RsiPeriod);
        var command = new GenerateFuturesRsiSignalCommand(
            new FuturesRsiSignalId(
                entityId.ContractId,
                entityId.ValueDate,
                entityId.TimePeriod,
                entityId.PeriodLength,
                new TimeOnly(10, 15)),
            5500m)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(
                ActorType.Command,
                GenerateFuturesRsiSignalCommand.Actor,
                GenerateFuturesRsiSignalCommand.Verb,
                entityId.Format())
        };
        var state = new FuturesRsiSignalCommandState();
        var stateFactory = Substitute.For<IEventSourceActorStateFactory>();
        stateFactory.CreateState<FuturesRsiSignalCommandState>().Returns(state);
        var eventDb = Substitute.For<IEventSourceActorDbContext>();
        eventDb.GetEventStreamIdAsync(command.StreamId, CancellationToken.None).Returns(streamId);
        var requiredHistory = configuration.RsiPeriod + configuration.RequiredRsiSamples;
        eventDb.MapReduceActorEventStreamAsync<
                FuturesRsiSignalCommandState,
                FuturesRsiSignalGeneratedEvent>(
                streamId,
                requiredHistory,
                Arg.Any<Action<IEnumerable<EventStreamReadModel>>>(), CancellationToken.None)
            .Returns(call =>
            {
                var rows = Enumerable.Range(0, requiredHistory)
                    .Select(index => new FuturesRsiSignalGeneratedEvent
                    {
                        EntityId = entityId,
                        FuturesRsiSignal = new FuturesRsiSignalReadModel(
                            entityId.ContractId,
                            entityId.ValueDate,
                            entityId.TimePeriod,
                            entityId.PeriodLength,
                            new TimeOnly(10, 0).Add(TimeSpan.FromSeconds(index * 15)),
                            5500m + index,
                            1m,
                            1m,
                            0m,
                            1m,
                            0m,
                            double.MaxValue,
                            100d,
                            0d,
                            0d)
                        {
                            PreviousRsi = 100d,
                            RegimeSlope = 0d,
                            IsWarm = index >= configuration.RsiPeriod
                        },
                        FuturesRsiCheckpoint = new FuturesRsiAccumulatorCheckpoint
                        {
                            PeriodLength = configuration.RsiPeriod,
                            PreviousClose = 5500m + index,
                            AverageGain = 1m,
                            AverageLoss = 0m,
                            CurrentRsi = 100d,
                            ChangeCount = index
                        }
                    })
                    .Select((generatedEvent, index) => new EventStreamReadModel
                    {
                        EventVersion = index + 1,
                        EventTypeName = typeof(FuturesRsiSignalGeneratedEvent).AssemblyQualifiedName!,
                        EventData = TomasAI.IFM.Shared.EventSourcing.EventLogMessagePackCodec.Shared.Serialize(generatedEvent)
                    })
                    .ToArray();
                call.Arg<Action<IEnumerable<EventStreamReadModel>>>()(rows);
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

        loadedState.FuturesRsiSignals.Should().HaveCount(requiredHistory);
        loadedState.FuturesRsiSignals.Count(signal => signal.IsWarm)
            .Should().Be(configuration.RequiredRsiSamples);
        loadedState.FuturesRsiCheckpoint.Should().NotBeNull();
        loadedState.FuturesRsiCheckpoint!.CurrentRsi.Should().Be(100d);
        await eventDb.Received(1).MapReduceActorEventStreamAsync<
            FuturesRsiSignalCommandState,
            FuturesRsiSignalGeneratedEvent>(
            streamId,
            requiredHistory,
            Arg.Any<Action<IEnumerable<EventStreamReadModel>>>(), CancellationToken.None);
    }
}
