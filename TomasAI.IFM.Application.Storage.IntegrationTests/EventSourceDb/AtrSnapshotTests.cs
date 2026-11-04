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

namespace TomasAI.IFM.Application.Storage.IntegrationTests.EventSourceDb;

public sealed class AtrSnapshotTests(EventSourceActorSnapshotRangeFixture fixture, ITestOutputHelper output)
    : IClassFixture<EventSourceActorSnapshotRangeFixture>
{
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
