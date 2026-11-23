using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using Xunit;
using Xunit.Abstractions;
using TomasAI.IFM.Application.MarketData.Databento.Historical;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.EventSourceDb;

public sealed class AtrSnapshotTests(EventSourceActorSnapshotRangeFixture fixture, ITestOutputHelper output)
    : IClassFixture<EventSourceActorSnapshotRangeFixture>
{
    [Fact]
    public async Task Overnight_seed_and_baseline_survive_database_restart_replay_and_catchup_without_reset()
    {
        var open = new DateTimeOffset(2026, 11, 19, 14, 30, 0, TimeSpan.Zero);
        var contract = "ES-" + Guid.NewGuid().ToString("N");
        var series = MarketSeriesIdentity.ForContract(contract);
        var windows = RsiHistoricalSeedWindowModel.Create(TimeFrameType.FiveMinutes, 49, open, new CmeFuturesMarketSessionCalendar());
        var bars = windows.Select((window, index) => new FuturesTradeSessionBarReadModel
        {
            ContractId = contract, MarketSeriesIdentity = series, ValueDate = window.ValueDate, TimeFrame = TimeFrameType.FiveMinutes,
            ObservationId = FuturesTradeSessionBarId.Create(series, TimeFrameType.FiveMinutes, window.EndUtc, index + 1),
            IntervalStartUtc = window.StartUtc, IntervalEndUtc = window.EndUtc,
            FirstMarketEventUtc = window.StartUtc.AddSeconds(1), LastMarketEventUtc = window.EndUtc.AddSeconds(-1),
            FirstSourceSequence = index + 1, LastSourceSequence = index + 1,
            Open = 6000, High = 6005 + index % 7, Low = 5998, Close = 6001,
            Volume = 100, TradeCount = 10, PriceVolumeSum = 600100,
            IsComplete = true, IsValid = true, SchemaVersion = 1, CalculationVersion = "test-overnight", CalculatedAtUtc = window.EndUtc,
            CalculationMethod = MarketSignalCalculationMethod.ClosedObservation
        }).ToArray();
        var entity = new FuturesAtrSignalEntityId(contract, new(2026, 11, 19), TimeFrameType.FiveMinutes, 14);
        var subject = new ActorSubject(ActorType.Command, StartFuturesAtrSignalCommand.Actor, StartFuturesAtrSignalCommand.Verb, entity.Format());
        var start = new StartFuturesAtrSignalCommand(entity) { Subject = subject, CommandId = Guid.NewGuid(), HistoricalSeed = bars[..48] };
        var state = new FuturesAtrSignalCommandState();
        Assert.True(start.Execute(state).Success);
        Assert.True(state.AtrSignal!.AtrBaseline > 0);
        var database = fixture.ActorEventDb;
        var initialCount = state.Events.Count;
        await database.SaveEventsAsync(start.StreamId, start.CommandId, new DomainEventCollection(state.Events), 0, CancellationToken.None);
        var factory = Substitute.For<IEventSourceActorStateFactory>();
        factory.CreateState<FuturesAtrSignalCommandState>().Returns(_ => new FuturesAtrSignalCommandState());
        var repository = new FuturesAtrSignalStateRepository(factory, database, Substitute.For<IActorService>(), Substitute.For<IDbContextFactory>(),
            Substitute.For<IEventProjector<FuturesAtrSignalCommandActor>>(), NullLogger<FuturesAtrSignalStateRepository>.Instance);
        var recovered = await repository.LoadStateAsync(start);
        Assert.Equal(state.AtrSignal.AtrValue, recovered.AtrSignal!.AtrValue);
        Assert.Equal(state.AtrSignal.AtrBaseline, recovered.AtrSignal.AtrBaseline);
        var retry = start with { CommandId = Guid.NewGuid(), HistoricalSeed = bars[1..] };
        Assert.True(retry.Execute(recovered).Success);
        Assert.False(Assert.Single(recovered.Events.OfType<FuturesAtrSignalStartedEvent>()).ResetForHistoricalSeed);
        Assert.Single(recovered.Events.OfType<FuturesAtrSignalGeneratedEvent>());
        Assert.Equal(49, recovered.FuturesAtrCheckpoint!.ObservationCount);
        var expected = new FuturesAtrSignalCommandState();
        Assert.True((start with { HistoricalSeed = bars }).Execute(expected).Success);
        Assert.Equal(expected.AtrSignal!.AtrValue, recovered.AtrSignal.AtrValue);
        Assert.Equal(expected.AtrSignal.AtrRatio, recovered.AtrSignal.AtrRatio);
        await database.SaveEventsAsync(start.StreamId, retry.CommandId, new DomainEventCollection(recovered.Events), initialCount, CancellationToken.None);
        var secondRestart = await repository.LoadStateAsync(start);
        Assert.Equal(recovered.FuturesAtrCheckpoint.LastIntervalEndUtc, secondRestart.FuturesAtrCheckpoint!.LastIntervalEndUtc);
        Assert.Equal(recovered.AtrSignal.AtrBaseline, secondRestart.AtrSignal!.AtrBaseline);
        Assert.True((retry with { CommandId = Guid.NewGuid() }).Execute(secondRestart).Success);
        Assert.Empty(secondRestart.Events.OfType<FuturesAtrSignalGeneratedEvent>());
        Assert.Equal(49, secondRestart.FuturesAtrCheckpoint.ObservationCount);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Restores_the_accumulator_and_last_signal(bool daily, bool legacyHead)
    {
        const int head = 512;
        var subject = new ActorSubject(ActorType.Command, "AtrCheckpointTest", "Generate", Guid.NewGuid().ToString("N"));
        ICommand command = daily
            ? new GenerateFuturesAtrDailySignalCommand { Subject = subject }
            : new GenerateFuturesAtrSignalCommand { Subject = subject };
        var checkpointType = daily ? typeof(FuturesAtrDailySignalGeneratedEvent) : typeof(FuturesAtrSignalGeneratedEvent);
        var events = Enumerable.Range(1, head).Select(CreateEvent);
        var database = fixture.ActorEventDb;
        await database.SaveEventsAsync(command.StreamId, Guid.NewGuid(), new DomainEventCollection(events), 0, CancellationToken.None);
        var factory = Substitute.For<IEventSourceActorStateFactory>();
        factory.CreateState<FuturesAtrSignalCommandState>().Returns(_ => new FuturesAtrSignalCommandState());
        var repository = new FuturesAtrSignalStateRepository(factory, database, Substitute.For<IActorService>(), Substitute.For<IDbContextFactory>(),
            Substitute.For<IEventProjector<FuturesAtrSignalCommandActor>>(),
            NullLogger<FuturesAtrSignalStateRepository>.Instance);

        var full = new List<double>();
        var snapshot = new List<double>();
        for (var sample = 0; sample < (legacyHead ? 1 : 20); sample++)
        {
            FuturesAtrSignalCommandState baseline;
            FuturesAtrSignalCommandState restored;
            if (sample % 2 == 0)
            {
                baseline = await ReplayFull();
                restored = await ReplaySnapshot();
            }
            else
            {
                restored = await ReplaySnapshot();
                baseline = await ReplayFull();
            }

            Assert.Equivalent(baseline.AtrSignal, restored.AtrSignal);
            Assert.Equivalent(baseline.FuturesAtrCheckpoint, restored.FuturesAtrCheckpoint);
            Assert.Equal(legacyHead ? head - 1 : head, restored.FuturesAtrCheckpoint!.LastSourceSequence);
            Assert.Equal(head, restored.CommittedStreamVersion);
            Assert.Empty(restored.Events);
        }

        if (!legacyHead)
        {
            var stream = await database.GetEventStreamIdAsync(command.StreamId, CancellationToken.None);
            var rows = new List<long>();
            await database.MapReduceActorEventStreamFromCreationWithCheckpointsAsync<FuturesAtrSignalCommandState>(
                stream, [checkpointType], page => rows.AddRange(page.Select(row => row.StreamVersion)));
            Assert.Equal(new long[] { 1, head }, rows);
            full.Sort();
            snapshot.Sort();
            output.WriteLine($"ATR (daily: {daily}): {head} -> {rows.Count} rows, " +
                $"median {(full[9] + full[10]) / 2:F2} -> {(snapshot[9] + snapshot[10]) / 2:F2} ms, " +
                $"p95 {full[18]:F2} -> {snapshot[18]:F2} ms.");
        }

        IEvent CreateEvent(int version)
        {
            var checkpoint = legacyHead && version == head ? null : new FuturesAtrAccumulatorCheckpoint
            {
                PeriodLength = 14, LastSourceSequence = version, PreviousClose = version, CurrentAtr = 2m
            };
            var signal = new FuturesAtrSignalReadModel { ContractId = "ES", PeriodLength = 14, AtrValue = version };
            return daily
                ? new FuturesAtrDailySignalGeneratedEvent { Id = Guid.NewGuid(), CommandId = Guid.NewGuid(),
                    FuturesAtrCheckpoint = checkpoint, FuturesAtrSignal = signal }
                : new FuturesAtrSignalGeneratedEvent { Id = Guid.NewGuid(), CommandId = Guid.NewGuid(),
                    FuturesAtrCheckpoint = checkpoint, FuturesAtrSignal = signal };
        }

        async Task<FuturesAtrSignalCommandState> ReplayFull()
        {
            var started = Stopwatch.GetTimestamp();
            var stream = await database.GetEventStreamIdAsync(command.StreamId, CancellationToken.None);
            var state = (FuturesAtrSignalCommandState)factory.CreateState<FuturesAtrSignalCommandState>();
            state.Id = subject.ThreadId;
            await database.MapReduceActorEventStreamFromCreationAsync<FuturesAtrSignalCommandState>(stream, state.ReplayEvents);
            full.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            return state;
        }

        async Task<FuturesAtrSignalCommandState> ReplaySnapshot()
        {
            var started = Stopwatch.GetTimestamp();
            var state = await repository.LoadStateAsync(command);
            snapshot.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            return state;
        }
    }
}
