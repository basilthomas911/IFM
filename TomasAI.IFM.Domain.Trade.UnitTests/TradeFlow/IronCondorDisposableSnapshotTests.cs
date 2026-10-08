using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function.State;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using PlanId = TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IronCondorTradePlanId;

namespace TomasAI.IFM.Domain.Trade.UnitTests.TradeFlow;

public sealed class IronCondorDisposableSnapshotTests
{
    static readonly DateTime Now = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Loads_only_one_latest_source_snapshot_without_retained_state_or_scylla_access()
    {
        var source = SourceDatabase();
        var original = Request();
        var persisted = Completed(original) with { EventId = 209, Plan = new() { PlanRevision = 4 } };
        source.MapReduceActorEventStreamAsync<IronCondorTradePlanFunctionState, IronCondorTradePlanUpdatedEvent>(
            42, 1, Arg.Any<Action<IEnumerable<EventStreamReadModel>>>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            call.ArgAt<Action<IEnumerable<EventStreamReadModel>>>(2)([Stored(persisted, 7)]);
            return ValueTask.CompletedTask;
        });
        var repository = Repository(source);
        var first = await repository.LoadStateAsync(original);
        first.IsCompleted.Should().BeTrue();
        first.CompletedEvent!.EventId.Should().Be(209);
        first.CommittedStreamVersion.Should().Be(7);
        first.LatestPlanRevision.Should().Be(4);
        first.Events.Should().BeEmpty();
        var next = original with { CommandId = Guid.NewGuid() };
        var second = await repository.LoadStateAsync(next);
        second.Should().NotBeSameAs(first);
        second.IsCompleted.Should().BeFalse();
        second.LatestPlanRevision.Should().Be(4);
        await source.Received(2).MapReduceActorEventStreamAsync<IronCondorTradePlanFunctionState, IronCondorTradePlanUpdatedEvent>(
            42, 1, Arg.Any<Action<IEnumerable<EventStreamReadModel>>>(), Arg.Any<CancellationToken>());
        source.ReceivedCalls().Should().OnlyContain(call =>
            call.GetMethodInfo().Name == nameof(IEventSourceActorDbContext.GetEventStreamIdAsync)
            || call.GetMethodInfo().Name == nameof(IEventSourceActorDbContext.MapReduceActorEventStreamAsync));
    }

    [Fact]
    public async Task Source_commit_precedes_projection_and_failed_projection_does_not_prevent_the_next_snapshot()
    {
        var source = SourceDatabase();
        var projector = Substitute.For<IEventProjector<FuturesIronCondorTradePositionCommandActor>>();
        var logger = Substitute.For<ILogger<IronCondorTradePlanFunctionStateRepository>>();
        var repository = Repository(source, projector, logger);
        IronCondorTradePlanUpdatedEvent? lastStored = null;
        long storedVersion = 0;
        source.MapReduceActorEventStreamAsync<IronCondorTradePlanFunctionState, IronCondorTradePlanUpdatedEvent>(
            42, 1, Arg.Any<Action<IEnumerable<EventStreamReadModel>>>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            call.ArgAt<Action<IEnumerable<EventStreamReadModel>>>(2)(lastStored is null ? [] : [Stored(lastStored, storedVersion)]);
            return ValueTask.CompletedTask;
        });
        source.SaveEventsAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<DomainEventCollection>(),
            Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            call.ArgAt<long>(3).Should().Be(storedVersion);
            var pending = (IronCondorTradePlanUpdatedEvent)call.ArgAt<DomainEventCollection>(2).Single();
            EventInitHelper.SetProperty(pending, nameof(IEvent.EventId), 1000 + ++storedVersion);
            lastStored = pending;
            return Task.FromResult(new DomainEventCollection([pending]));
        });
        projector.DomainEventsProjectionAsync(Arg.Any<DomainEventCollection>()).Returns(call =>
        {
            var snapshot = call.Arg<DomainEventCollection>().Single();
            snapshot.Should().BeSameAs(lastStored);
            snapshot.EventId.Should().Be(1000 + storedVersion);
            throw new IOException("Scylla submission failed");
        });
        var firstRequest = Request();
        var first = await repository.LoadStateAsync(firstRequest);
        first.TryComplete(Completed(firstRequest), firstRequest).Should().BeTrue();
        await repository.SaveCompletedStateAsync(Substitute.For<IFunctionActorContext>(), first, firstRequest);
        first.Events.Should().BeEmpty();
        var next = firstRequest with { CommandId = Guid.NewGuid() };
        var second = await repository.LoadStateAsync(next);
        second.CommittedStreamVersion.Should().Be(1);
        second.TryComplete(Completed(next), next).Should().BeTrue();
        await repository.SaveCompletedStateAsync(Substitute.For<IFunctionActorContext>(), second, next);
        await source.Received(2).SaveEventsAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<DomainEventCollection>(),
            Arg.Any<long>(), Arg.Any<CancellationToken>());
        await projector.Received(2).DomainEventsProjectionAsync(Arg.Is<DomainEventCollection>(events => events.Count == 1));
        logger.ReceivedCalls().Count(call => call.GetMethodInfo().Name == nameof(ILogger.Log)).Should().Be(2);
        storedVersion.Should().Be(2);
        var duplicate = await repository.LoadStateAsync(next);
        duplicate.IsCompleted.Should().BeTrue();
        duplicate.Matches(next).Should().BeTrue();
        // Loading the committed snapshot never resubmits the previously failed Scylla projection.
        projector.ReceivedCalls().Should().HaveCount(2);
    }

    [Fact]
    public async Task Failed_source_commit_does_not_submit_an_uncommitted_snapshot()
    {
        var source = SourceDatabase();
        source.SaveEventsAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<DomainEventCollection>(),
            Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(Task.FromException<DomainEventCollection>(new IOException("source unavailable")));
        var projector = Substitute.For<IEventProjector<FuturesIronCondorTradePositionCommandActor>>();
        var repository = Repository(source, projector);
        var request = Request();
        var state = await repository.LoadStateAsync(request);
        state.TryComplete(Completed(request), request).Should().BeTrue();
        var save = async () => await repository.SaveCompletedStateAsync(Substitute.For<IFunctionActorContext>(), state, request);
        await save.Should().ThrowAsync<IOException>();
        projector.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task Calculation_uses_current_position_profit_and_the_single_latest_source_stop_without_history()
    {
        var original = Request();
        var request = original with { Position = original.Position with { UnrealizedPnl = 2, RealizedPnl = 1 },
            IronCondorTradePlanInputs = new() { AverageTradePnl = 17, StopLossLimit = 0.25, ContractCashMultiplier = 50, OpeningCommission = 1 } };
        var context = Substitute.For<IIronCondorTradePlanFunctionContext>();
        context.Algorithm.Returns(new IronCondorTradePlanAlgorithm());
        var state = new IronCondorTradePlanFunctionState().Prepare(request);
        var result = await request.ExecuteAsync(state, context, input => input.Complete(TimeProvider.System), CancellationToken.None);
        result.Completed!.Plan.PlanRevision.Should().Be(1);
        result.Completed.Plan.CalculatedAtUtc.Should().Be(request.RequestedAtUtc);
        result.Completed.Plan.IronCondorTradePlanSnapshot!.IronCondorTradePlanInputs!.AverageTradePnl.Should().Be(149);
        result.Completed.Plan.IronCondorTradePlanSnapshot.IronCondorTradePlanInputs.StopLossLimit.Should().Be(0);
        var fresh = new IronCondorTradePlanFunctionState().Prepare(request);
        var repeated = await request.ExecuteAsync(fresh, context, input => input.Complete(TimeProvider.System), CancellationToken.None);
        repeated.Completed!.Plan.ContentHash.Should().Be(result.Completed.Plan.ContentHash);
        var next = request with { RequestedAtUtc = Now.AddTicks(1), CommandId = Guid.NewGuid() };
        var restored = new IronCondorTradePlanFunctionState();
        var lastAccepted = result.Completed with { Plan = result.Completed.Plan with
            { IronCondorTradePlanSnapshot = result.Completed.Plan.IronCondorTradePlanSnapshot! with { StopLossLimit = 0.25 } } };
        restored.ReplayEvents(new[] { Stored(lastAccepted, 1) });
        restored.Prepare(next);
        var later = await next.ExecuteAsync(restored, context,
            input => input.Complete(TimeProvider.System), CancellationToken.None);
        later.Completed!.Plan.PlanRevision.Should().BeGreaterThan(result.Completed.Plan.PlanRevision);
        later.Completed.Plan.IronCondorTradePlanSnapshot!.IronCondorTradePlanInputs!.StopLossLimit.Should().Be(0.25);
    }

    [Theory]
    [InlineData("time")]
    [InlineData("generation")]
    [InlineData("sequence")]
    public async Task Delayed_observations_cannot_replace_the_single_latest_source_snapshot(string staleField)
    {
        var original = Request();
        var accepted = Completed(original) with { Plan = new() { Position = original.Position,
            CalculatedAtUtc = Now, PlanRevision = 4 } };
        var request = original with { CommandId = Guid.NewGuid(),
            RequestedAtUtc = staleField == "time" ? Now.AddTicks(-1) : Now,
            Position = original.Position with
            {
                RouteGeneration = staleField == "generation" ? 0 : 1,
                PositionSequence = staleField == "sequence" ? 0 : 1
            } };
        var state = new IronCondorTradePlanFunctionState();
        state.ReplayEvents([Stored(accepted, 7)]);
        state.Prepare(request);
        var context = Substitute.For<IIronCondorTradePlanFunctionContext>();
        var execute = async () => await request.ExecuteAsync(state, context,
            input => input.Complete(TimeProvider.System), CancellationToken.None);
        await execute.Should().ThrowAsync<InvalidOperationException>().WithMessage("*OBSERVATION.SUPERSEDED*");
        state.Events.Should().BeEmpty();
        state.LatestPlanRevision.Should().Be(4);
        _ = context.DidNotReceive().Algorithm;
    }

    [Fact]
    public async Task Projection_uses_committed_payload_without_source_readback_or_validation_and_continues_after_write_failure()
    {
        var database = Substitute.For<IEventSourceActorDbContext>();
        var queue = Substitute.For<IDurableReplayQueue>();
        var attempts = 0;
        var projector = new SnapshotProjector(database, queue, (_, _) =>
        {
            if (++attempts == 1) throw new IOException("snapshot write failed");
            return Task.CompletedTask;
        });
        await projector.ProcessDomainEventAsync(Completed(Request()));
        await projector.ProcessDomainEventAsync(Completed(Request()));
        attempts.Should().Be(2);
        database.ReceivedCalls().Should().BeEmpty();
        queue.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task Full_snapshot_queue_rejects_immediately_without_waiting_for_the_active_write()
    {
        var writing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var projector = new SnapshotProjector(Substitute.For<IEventSourceActorDbContext>(), Substitute.For<IDurableReplayQueue>(),
            async (_, _) => { writing.TrySetResult(); await release.Task; });
        await projector.StartAsync(Substitute.For<ICommandActorContext>());
        try
        {
            await projector.DomainEventsProjectionAsync([Completed(Request())]);
            await writing.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await projector.DomainEventsProjectionAsync([Completed(Request())]);
            var watch = Stopwatch.StartNew();
            var third = async () => await projector.DomainEventsProjectionAsync([Completed(Request())]);
            await third.Should().ThrowAsync<InvalidOperationException>().WithMessage("*snapshot*admission*failed*");
            watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));
        }
        finally { release.TrySetResult(); await projector.StopAsync(); }
    }

    [Fact]
    public void Snapshot_descriptor_rejects_durable_replay_and_persistence_completion()
    {
        EventProjectionDescriptor Create(bool durable, bool terminal) => new(typeof(IronCondorTradePlanUpdatedEvent),
            EventProjectionIdempotencyStrategy.NaturalKeyMutation, (_, _) => throw new NotSupportedException(),
            _ => null, (_, _) => null, useDurableReplay: durable, publishTerminalEvent: terminal,
            applySnapshotAsync: (_, _) => ValueTask.FromResult(new EventProjectionApplyResult(EventProjectionApplyOutcome.Applied)));
        FluentActions.Invoking(() => Create(true, false)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => Create(false, true)).Should().Throw<ArgumentException>();
    }

    sealed class SnapshotProjector : ConventionalEventProjector<FuturesIronCondorTradePositionCommandActor>
    {
        readonly EventProjectionDescriptor descriptor;
        public SnapshotProjector(IEventSourceActorDbContext source, IDurableReplayQueue queue,
            Func<IronCondorTradePlanUpdatedEvent, CancellationToken, Task> write)
            : base(queue, source, Substitute.For<IBlackboardService>(), Substitute.For<ILogger>(),
                new EventProjectorReliabilityOptions { NonDurableQueueCapacity = 1 })
            => descriptor = DescribeSnapshot<IronCondorTradePlanUpdatedEvent, PlanId>(write, publishProcessingEvent: false);
        public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => [descriptor];
        public override IReadOnlyCollection<Type> ProjectedEventTypes => [typeof(IronCondorTradePlanUpdatedEvent)];
    }

    static IEventSourceActorDbContext SourceDatabase()
    {
        var source = Substitute.For<IEventSourceActorDbContext>();
        source.GetEventStreamIdAsync(Arg.Any<string>()).Returns(42L);
        source.GetEventStreamIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(42L);
        return source;
    }

    static IronCondorTradePlanFunctionStateRepository Repository(IEventSourceActorDbContext source,
        IEventProjector<FuturesIronCondorTradePositionCommandActor>? projector = null,
        ILogger<IronCondorTradePlanFunctionStateRepository>? logger = null) => new(
            Substitute.For<IEventSourceActorStateFactory>(), source, Substitute.For<IActorService>(),
            projector ?? Substitute.For<IEventProjector<FuturesIronCondorTradePositionCommandActor>>(),
            logger ?? Substitute.For<ILogger<IronCondorTradePlanFunctionStateRepository>>());

    static EventStreamReadModel Stored(IronCondorTradePlanUpdatedEvent snapshot, long streamVersion) => new()
    {
        EventVersion = snapshot.EventId, StreamVersion = streamVersion,
        EventTypeName = typeof(IronCondorTradePlanUpdatedEvent).AssemblyQualifiedName!,
        EventData = EventLogMessagePackCodec.Shared.Serialize(snapshot)
    };

    static UpdateIronCondorTradePlanCommand Request()
    {
        var trade = new TradeEntityId(1, 2, 3, 4);
        var position = new StrategyPositionSnapshot
        {
            Id = StrategyPositionId.Create(trade, TradeStrategyKind.IronCondor), StrategyKind = TradeStrategyKind.IronCondor,
            PositionSequence = 1, RouteGeneration = 1, IsOpen = true, AsOfUtc = Now,
            Legs = Enumerable.Range(1, 4).Select(index => new StrategyPositionLeg
            {
                TradeLegId = Guid.NewGuid(), ContractId = $"OPTION-{index}", AssetFamily = TradeAssetFamily.FuturesOption,
                SignedQuantity = index % 2 == 0 ? -1 : 1, OpeningPrice = 10, CurrentPrice = 11, LastPriceAtUtc = Now
            }).ToArray()
        };
        var id = new PlanId(position.Id, DateOnly.FromDateTime(Now));
        return new() { CommandId = Guid.NewGuid(), EntityId = id, Position = position, RequestedAtUtc = Now,
            SourceEventId = Guid.NewGuid(), Subject = new(ActorType.Function, UpdateIronCondorTradePlanCommand.Actor,
                UpdateIronCondorTradePlanCommand.Verb, id.Format()) };
    }

    static IronCondorTradePlanUpdatedEvent Completed(UpdateIronCondorTradePlanCommand request) => new()
    {
        CommandId = request.CommandId, EntityId = request.EntityId, Subject = request.Subject,
        RequestFingerprint = request.Fingerprint(), Plan = new() { Position = request.Position, PlanRevision = 1 }
    };
}
