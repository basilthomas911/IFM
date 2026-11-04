using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVxTermStructureSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using Xunit;
using Xunit.Abstractions;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.EventSourceDb;

public sealed class VxTermStructureSnapshotTests(EventSourceActorSnapshotRangeFixture fixture, ITestOutputHelper output)
    : IClassFixture<EventSourceActorSnapshotRangeFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_leg_only_head_preserves_the_last_paired_signal(bool legacyHead)
    {
        const int head = 512;
        var subject = new ActorSubject(ActorType.Command, "VxCheckpointTest", "Update", Guid.NewGuid().ToString("N"));
        var command = Substitute.For<ICommand>();
        command.Subject.Returns(subject);
        command.StreamId.Returns($"VxCheckpointTest.{subject.EntityId}");
        var paired = new FuturesVxTermStructureSignalReadModel { FrontVxPrice = 20m, BackVxPrice = 21m, IsValid = true };
        var events = Enumerable.Range(1, head).Select(version => new FuturesVxTermStructureSignalUpdatedEvent
        {
            Id = Guid.NewGuid(),
            CommandId = Guid.NewGuid(),
            FuturesVxTermStructureCheckpoint = new() { PreviousFrontBackRatio = version },
            FuturesVxTermStructureSignal = version == 2 ? paired : null,
            HasFullStateSnapshot = !legacyHead,
            RetainedSignal = !legacyHead && version > 2 ? paired : null
        });
        var database = fixture.ActorEventDb;
        await database.SaveEventsAsync(command.StreamId, Guid.NewGuid(), new DomainEventCollection(events), 0, CancellationToken.None);
        var factory = Substitute.For<IEventSourceActorStateFactory>();
        factory.CreateState<FuturesVxTermStructureSignalCommandState>().Returns(_ => new FuturesVxTermStructureSignalCommandState());
        var repository = new FuturesVxTermStructureSignalStateRepository(factory, database, Substitute.For<IActorService>(),
            Substitute.For<IEventProjector<FuturesVxTermStructureSignalCommandActor>>(),
            NullLogger<FuturesVxTermStructureSignalStateRepository>.Instance);

        var full = new List<double>();
        var snapshot = new List<double>();
        for (var sample = 0; sample < (legacyHead ? 1 : 20); sample++)
        {
            FuturesVxTermStructureSignalCommandState baseline;
            FuturesVxTermStructureSignalCommandState restored;
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

            Assert.Equivalent(paired, restored.FuturesVxTermStructureSignal);
            Assert.Equivalent(baseline.FuturesVxTermStructureSignal, restored.FuturesVxTermStructureSignal);
            Assert.Equivalent(baseline.FuturesVxTermStructureCheckpoint, restored.FuturesVxTermStructureCheckpoint);
            Assert.Equal(head, restored.CommittedStreamVersion);
            Assert.Empty(restored.Events);
        }

        if (!legacyHead)
        {
            var stream = await database.GetEventStreamIdAsync(command.StreamId, CancellationToken.None);
            var rows = new List<long>();
            await database.MapReduceActorEventStreamFromCreationWithCheckpointsAsync<FuturesVxTermStructureSignalCommandState>(
                stream, [typeof(FuturesVxTermStructureSignalUpdatedEvent)], page => rows.AddRange(page.Select(row => row.StreamVersion)));
            Assert.Equal(new long[] { 1, head }, rows);
            full.Sort();
            snapshot.Sort();
            output.WriteLine($"VX term structure: {head} -> {rows.Count} rows, " +
                $"median {(full[9] + full[10]) / 2:F2} -> {(snapshot[9] + snapshot[10]) / 2:F2} ms, " +
                $"p95 {full[18]:F2} -> {snapshot[18]:F2} ms.");
        }

        async Task<FuturesVxTermStructureSignalCommandState> ReplayFull()
        {
            var started = Stopwatch.GetTimestamp();
            var stream = await database.GetEventStreamIdAsync(command.StreamId, CancellationToken.None);
            var state = (FuturesVxTermStructureSignalCommandState)factory.CreateState<FuturesVxTermStructureSignalCommandState>();
            state.Id = subject.ThreadId;
            await database.MapReduceActorEventStreamFromCreationAsync<FuturesVxTermStructureSignalCommandState>(stream, state.ReplayEvents);
            full.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            return state;
        }

        async Task<FuturesVxTermStructureSignalCommandState> ReplaySnapshot()
        {
            var started = Stopwatch.GetTimestamp();
            var state = await repository.LoadStateAsync(command);
            snapshot.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            return state;
        }
    }
}
