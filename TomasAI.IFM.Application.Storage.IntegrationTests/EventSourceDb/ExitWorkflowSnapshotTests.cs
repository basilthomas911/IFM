using System.Diagnostics;
using TomasAI.IFM.Domain.Trade.Shared;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Workflow.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Workflow.Command.State;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Workflow.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Workflow.Command.State;
using TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.Command.State;
using TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Model;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using Xunit;
using Xunit.Abstractions;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.EventSourceDb;

public sealed class ExitWorkflowSnapshotTests(EventSourceActorSnapshotRangeFixture fixture, ITestOutputHelper output)
    : IClassFixture<EventSourceActorSnapshotRangeFixture>
{
    [Fact]
    public Task Futures_workflow_loads_latest_start() =>
        Verify<FuturesExitPositionWorkflowCommandState>((factory, actors) =>
            new FuturesExitPositionWorkflowStateRepository(factory, fixture.ActorEventDb, actors,
                Substitute.For<IEventProjector<FuturesExitPositionWorkflowCommandActor>>(),
                NullLogger<FuturesExitPositionWorkflowStateRepository>.Instance));

    [Fact]
    public Task Iron_condor_workflow_loads_latest_start() =>
        Verify<IronCondorExitPositionWorkflowCommandState>((factory, actors) =>
            new IronCondorExitPositionWorkflowStateRepository(factory, fixture.ActorEventDb, actors,
                Substitute.For<IEventProjector<IronCondorExitPositionWorkflowCommandActor>>(),
                NullLogger<IronCondorExitPositionWorkflowStateRepository>.Instance));

    [Fact]
    public Task Vertical_spread_workflow_loads_latest_start() =>
        Verify<VerticalSpreadExitPositionWorkflowCommandState>((factory, actors) =>
            new VerticalSpreadExitPositionWorkflowStateRepository(factory, fixture.ActorEventDb, actors,
                Substitute.For<IEventProjector<VerticalSpreadExitPositionWorkflowCommandActor>>(),
                NullLogger<VerticalSpreadExitPositionWorkflowStateRepository>.Instance));

    private async Task Verify<TState>(
        Func<IEventSourceActorStateFactory, IActorService, IEventSourceActorStateRepository<TState>> create)
        where TState : ExitPositionWorkflowCommandStateBase<TState>, new()
    {
        const int head = 12;
        const int samples = 20;
        var command = Substitute.For<ICommand>();
        var entity = new ExitPositionWorkflowId(new StrategyPositionId(new TradeEntityId(1, 2, 3, 4), Guid.NewGuid()), new DateOnly(2026, 11, 6), Guid.NewGuid());
        var subject = new ActorSubject(ActorType.Command, "ExitWorkflowSnapshotTest", "Start", entity.Format());
        command.Subject.Returns(subject);
        command.StreamId.Returns(subject.StreamId);
        var kind = typeof(TState) == typeof(FuturesExitPositionWorkflowCommandState) ? TradeStrategyKind.FuturesOutright
            : typeof(TState) == typeof(IronCondorExitPositionWorkflowCommandState) ? TradeStrategyKind.IronCondor
            : TradeStrategyKind.VerticalSpread;
        var events = Enumerable.Range(1, head).Select(revision => new ExitPositionWorkflowStartedEvent
        {
            Id = Guid.NewGuid(), CommandId = Guid.NewGuid(), StrategyKind = kind, EntityId = entity,
            Subject = new(ActorType.Event, "ExitWorkflowSnapshotTest", ExitPositionWorkflowStartedEvent.Verb, entity.Format()),
            SourcePlanEventId = Guid.NewGuid(), ExitPlan = new() { PlanRevision = revision }
        });
        var database = fixture.ActorEventDb;
        await database.SaveEventsAsync(command.StreamId, Guid.NewGuid(), new DomainEventCollection(events), 0, CancellationToken.None);
        var factory = Substitute.For<IEventSourceActorStateFactory>();
        factory.CreateState<TState>().Returns(_ => new TState());
        var repository = create(factory, Substitute.For<IActorService>());

        var baselineTimes = new double[samples];
        var checkpointTimes = new double[samples];
        for (var i = 0; i < samples; i++)
        {
            TState baseline;
            TState checkpoint;
            if (i % 2 == 0)
            {
                baseline = await ReplayFull(i);
                checkpoint = await ReplayCheckpoint(i);
            }
            else
            {
                checkpoint = await ReplayCheckpoint(i);
                baseline = await ReplayFull(i);
            }

            Assert.Equal(head, checkpoint.Started!.ExitPlan.PlanRevision);
            Assert.Equivalent(baseline.Started, checkpoint.Started);
            Assert.Equal(head, checkpoint.CommittedStreamVersion);
            Assert.Empty(checkpoint.Events);
        }

        var stream = await database.GetEventStreamIdAsync(command.StreamId, CancellationToken.None);
        var versions = new List<long>();
        await database.MapReduceActorEventStreamFromCreationWithCheckpointsAsync<TState>(
            stream, [typeof(ExitPositionWorkflowStartedEvent)], rows => versions.AddRange(rows.Select(row => row.StreamVersion)));
        Assert.Equal(new long[] { 1, head }, versions);
        Array.Sort(baselineTimes);
        Array.Sort(checkpointTimes);
        output.WriteLine($"{typeof(TState).Name}: {head} -> {versions.Count} rows, " +
            $"median {(baselineTimes[9] + baselineTimes[10]) / 2:F2} -> {(checkpointTimes[9] + checkpointTimes[10]) / 2:F2} ms, " +
            $"p95 {baselineTimes[18]:F2} -> {checkpointTimes[18]:F2} ms.");

        async Task<TState> ReplayFull(int sample)
        {
            var start = Stopwatch.GetTimestamp();
            var id = await database.GetEventStreamIdAsync(command.StreamId, CancellationToken.None);
            var state = (TState)factory.CreateState<TState>();
            state.Id = command.Subject.ThreadId;
            await database.MapReduceActorEventStreamFromCreationAsync<TState>(id, state.ReplayEvents);
            baselineTimes[sample] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            return state;
        }

        async Task<TState> ReplayCheckpoint(int sample)
        {
            var start = Stopwatch.GetTimestamp();
            var state = await repository.LoadStateAsync(command);
            checkpointTimes[sample] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            return state;
        }
    }
}
