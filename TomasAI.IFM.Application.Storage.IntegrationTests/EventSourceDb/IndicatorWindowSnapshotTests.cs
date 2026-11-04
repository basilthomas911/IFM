using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Storage;
using Xunit;
using Xunit.Abstractions;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.EventSourceDb;

public sealed class IndicatorWindowSnapshotTests(EventSourceActorSnapshotRangeFixture fixture, ITestOutputHelper output)
    : IClassFixture<EventSourceActorSnapshotRangeFixture>
{
    [Fact]
    public async Task Rsi_preserves_wider_history_after_a_narrow_snapshot()
    {
        var factory = Factory<FuturesRsiSignalCommandState>();
        var projector = Substitute.For<IEventProjector<FuturesRsiSignalCommandActor>>();
        var repository = new FuturesRsiSignalStateRepository(factory, fixture.ActorEventDb, Substitute.For<IActorService>(),
            DatabaseFactory(), projector, NullLogger<FuturesRsiSignalStateRepository>.Instance);
        await Verify<FuturesRsiSignalCommandState, FuturesRsiSignalStateSnapshottedEvent>(
            repository,
            (subject, window) => new GenerateFuturesRsiDailySignalCommand
            { Subject = subject, EntityId = new("ES", TimeFrameType.Daily, window), CommandId = Guid.NewGuid() },
            i => new FuturesRsiSignalGeneratedEvent
            {
                Id = Guid.NewGuid(), CommandId = Guid.NewGuid(),
                FuturesRsiSignal = new() { ContractId = "ES", RSI = i },
                FuturesRsiCheckpoint = new() { LastSourceSequence = i }
            },
            () => new FuturesRsiSignalStoppedEvent { Id = Guid.NewGuid() },
            (state, window) => state.LimitReplayWindow(window),
            state => state.FuturesRsiSignals.Select(signal => signal.RSI),
            (before, after) => Assert.Equivalent(before.FuturesRsiCheckpoint, after.FuturesRsiCheckpoint));
        await projector.Received(2).DomainEventsProjectionAsync(Arg.Is<DomainEventCollection>(
            events => events.Count == 1 && events[0] is FuturesRsiSignalStoppedEvent));
    }

    [Fact]
    public async Task Adx_preserves_wider_history_after_a_narrow_snapshot()
    {
        var factory = Factory<FuturesAdxSignalCommandState>();
        var projector = Substitute.For<IEventProjector<FuturesAdxSignalCommandActor>>();
        var repository = new FuturesAdxSignalStateRepository(factory, fixture.ActorEventDb, Substitute.For<IActorService>(),
            DatabaseFactory(), projector, NullLogger<FuturesAdxSignalStateRepository>.Instance);
        await Verify<FuturesAdxSignalCommandState, FuturesAdxSignalStateSnapshottedEvent>(
            repository,
            (subject, window) => new GenerateFuturesAdxDailySignalCommand
            { Subject = subject, EntityId = new("ES", TimeFrameType.Daily, window), CommandId = Guid.NewGuid() },
            i => new FuturesAdxSignalDailyFixture(i).Event,
            () => new FuturesAdxSignalStoppedEvent { Id = Guid.NewGuid() },
            (state, window) => state.LimitReplayWindow(window),
            state => state.AdxSignals.Select(signal => signal.AdxValue),
            (before, after) => Assert.Equivalent(before.AdxSignal, after.AdxSignal));
        await projector.Received(2).DomainEventsProjectionAsync(Arg.Is<DomainEventCollection>(
            events => events.Count == 1 && events[0] is FuturesAdxSignalStoppedEvent));
    }

    private async Task Verify<TState, TSnapshot>(IEventSourceActorStateRepository<TState> repository,
        Func<ActorSubject, int, ICommand> create, Func<int, IEvent> generated, Func<IEvent> stopped,
        Action<TState, int> setWindow, Func<TState, IEnumerable<double>> values, Action<TState, TState> assertState)
        where TState : BaseEventSourceActorState<TState>, new()
        where TSnapshot : IEvent
    {
        const int narrow = 14;
        const int wide = 64;
        var subject = new ActorSubject(ActorType.Command, "IndicatorWindowSnapshot", "Generate", Guid.NewGuid().ToString("N"));
        var narrowCommand = create(subject, narrow);
        var wideCommand = create(subject, wide);
        var database = fixture.ActorEventDb;
        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var transactions = new PostgresEventTransaction(new DbConnectionSettings()
            .Add(EventSourceActorDbContext.EventSourceActorDbConnection, database.ConnectionString, "System.Data.Postgres"));
        context.Container.Returns(container);
        container.Resolve<IPostgresEventTransaction>().Returns(transactions);

        await Seed(1, 127, 0);
        var narrowState = await repository.LoadStateAsync(narrowCommand);
        Assert.Equal(narrow, values(narrowState).Count());
        Assert.True(narrowState.Update(stopped(), narrowCommand));
        await repository.SaveStateAsync(context, narrowState, narrowCommand);
        Assert.Equal(129, narrowState.CommittedStreamVersion);

        var fallback = await repository.LoadStateAsync(wideCommand);
        Assert.Equal(Enumerable.Range(64, wide).Select(value => (double)value), values(fallback));
        Assert.Equal(129, fallback.CommittedStreamVersion);

        await Seed(128, 126, 129);
        var wideState = await repository.LoadStateAsync(wideCommand);
        Assert.Equal(wide, values(wideState).Count());
        Assert.True(wideState.Update(stopped(), wideCommand));
        await repository.SaveStateAsync(context, wideState, wideCommand);
        Assert.Equal(257, wideState.CommittedStreamVersion);
        await Seed(254, 1, 257);

        var narrowRestored = await repository.LoadStateAsync(narrowCommand);
        Assert.Equal(Enumerable.Range(241, narrow).Select(value => (double)value), values(narrowRestored));
        var full = new List<double>();
        var checkpoint = new List<double>();
        for (var sample = 0; sample < 20; sample++)
        {
            TState baseline;
            TState restored;
            if (sample % 2 == 0)
            {
                baseline = await ReplayFull();
                restored = await Restore();
            }
            else
            {
                restored = await Restore();
                baseline = await ReplayFull();
            }

            Assert.Equal(258, restored.CommittedStreamVersion);
            Assert.Equal(Enumerable.Range(191, wide).Select(value => (double)value), values(restored));
            Assert.Equal(values(baseline), values(restored));
            assertState(baseline, restored);
        }

        var stream = await database.GetEventStreamIdAsync(wideCommand.StreamId, CancellationToken.None);
        var rows = new List<long>();
        await database.MapReduceActorEventStreamFromCreationWithCheckpointsAsync<TState>(
            stream, [typeof(TSnapshot)], page => rows.AddRange(page.Select(row => row.StreamVersion)));
        Assert.Equal(new long[] { 1, 257, 258 }, rows);
        full.Sort();
        checkpoint.Sort();
        output.WriteLine($"{typeof(TState).Name}: 258 -> 3 rows, median " +
            $"{(full[9] + full[10]) / 2:F2} -> {(checkpoint[9] + checkpoint[10]) / 2:F2} ms, " +
            $"p95 {full[18]:F2} -> {checkpoint[18]:F2} ms.");

        Task<DomainEventCollection> Seed(int first, int count, long version) =>
            database.SaveEventsAsync(wideCommand.StreamId, Guid.NewGuid(),
                new DomainEventCollection(Enumerable.Range(first, count).Select(generated)), version, CancellationToken.None);

        async Task<TState> ReplayFull()
        {
            var start = Stopwatch.GetTimestamp();
            var id = await database.GetEventStreamIdAsync(wideCommand.StreamId, CancellationToken.None);
            var state = new TState { Id = subject.ThreadId };
            setWindow(state, wide);
            await database.MapReduceActorEventStreamFromCreationAsync<TState>(id, state.ReplayEvents);
            full.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            return state;
        }

        async Task<TState> Restore()
        {
            var start = Stopwatch.GetTimestamp();
            var state = await repository.LoadStateAsync(wideCommand);
            checkpoint.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            return state;
        }
    }

    private static IEventSourceActorStateFactory Factory<TState>() where TState : BaseEventSourceActorState<TState>, new()
    {
        var factory = Substitute.For<IEventSourceActorStateFactory>();
        factory.CreateState<TState>().Returns(_ => new TState());
        return factory;
    }

    private IDbContextFactory DatabaseFactory()
    {
        var factory = Substitute.For<IDbContextFactory>();
        var market = Substitute.For<IMarketDataDbContext>();
        market.ProviderName.Returns("System.Data.Postgres");
        market.ConnectionString.Returns(fixture.ActorEventDb.ConnectionString);
        factory.MarketDataDb.Returns(market);
        return factory;
    }

    private sealed class FuturesAdxSignalDailyFixture(int sequence)
    {
        public IEvent Event => new FuturesAdxDailySignalGeneratedEvent
        {
            Id = Guid.NewGuid(), CommandId = Guid.NewGuid(),
            FuturesAdxSignal = new() { ContractId = "ES", AdxValue = sequence }
        };
    }
}
