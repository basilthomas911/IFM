using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.EventProjector;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Projection;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime;

public sealed class WorkflowProjectionOrderingTests
{
    [Fact]
    public async Task Independent_projection_writes_overlap_before_any_write_completes()
    {
        using var fixture = new Fixture();
        var snapshot = Snapshot();
        var gates = new[] { "timeline", "start", "detail", "entity", "status" }
            .Select(kind => fixture.Hold(kind, 1)).ToArray();

        var projection = fixture.ApplyAsync(snapshot);
        await Task.WhenAll(gates.Select(gate => gate.Entered.Task)).WaitAsync(TimeSpan.FromSeconds(10));

        fixture.Started.Select(x => x.Kind).Should().BeEquivalentTo(
            ["timeline", "start", "detail", "entity", "status"]);
        fixture.Completed.Should().BeEmpty();
        fixture.Cache.TryGet(snapshot.EntityId.Format(), out _).Should().BeFalse();
        fixture.Notifications.Should().BeEmpty();

        foreach (var gate in gates) gate.Release.TrySetResult();
        await projection.WaitAsync(TimeSpan.FromSeconds(10));
        fixture.Completed.Select(x => x.Kind).Should().Contain("active");
        fixture.Notifications.Should().ContainSingle();
        fixture.UiNotifications.Should().ContainSingle();
    }

    [Theory]
    [InlineData("timeline")]
    [InlineData("start")]
    [InlineData("detail")]
    [InlineData("entity")]
    [InlineData("status")]
    [InlineData("active")]
    public async Task Cache_and_notification_wait_for_every_projection_write(string pendingWrite)
    {
        using var fixture = new Fixture();
        var snapshot = Snapshot();
        var gate = fixture.Hold(pendingWrite, 1);
        var projection = fixture.ApplyAsync(snapshot);
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        projection.IsCompleted.Should().BeFalse();
        fixture.Cache.TryGet(snapshot.EntityId.Format(), out _).Should().BeFalse();
        fixture.Notifications.Should().BeEmpty();
        if (pendingWrite != "active")
            fixture.Started.Should().NotContain(x => x.Kind == "active",
                "an active Query cache miss must not observe a revision before its supporting writes finish");

        gate.Release.TrySetResult();
        await projection.WaitAsync(TimeSpan.FromSeconds(10));

        fixture.Completed.Select(x => x.Kind).Should().BeEquivalentTo(
            ["timeline", "start", "detail", "entity", "status", "active"]);
        fixture.Cache.TryGet(snapshot.EntityId.Format(), out var active).Should().BeTrue();
        active!.WorkflowRevision.Should().Be(1);
        fixture.Notifications.Select(x => x.WorkflowRevision).Should().Equal(1);
        fixture.UiNotifications.Select(x => x.WorkflowRevision).Should().Equal(1);
    }

    [Theory]
    [InlineData("timeline")]
    [InlineData("start")]
    [InlineData("detail")]
    [InlineData("entity")]
    [InlineData("status")]
    [InlineData("active")]
    public async Task Failed_write_never_publishes_cache_or_notification(string failedWrite)
    {
        using var fixture = new Fixture { FailedWrite = failedWrite };
        var snapshot = Snapshot();

        var apply = async () => await fixture.ApplyAsync(snapshot);

        await apply.Should().ThrowAsync<InvalidOperationException>().WithMessage("projection write failed");
        fixture.Cache.TryGet(snapshot.EntityId.Format(), out _).Should().BeFalse();
        fixture.Notifications.Should().BeEmpty();
        fixture.UiNotifications.Should().BeEmpty();
        if (failedWrite != "active")
            fixture.Started.Should().NotContain(x => x.Kind == "active",
                "a failed supporting write must not publish the active row to Query callers");
    }

    [Theory]
    [InlineData("timeline")]
    [InlineData("detail")]
    [InlineData("entity")]
    [InlineData("status")]
    [InlineData("delete")]
    public async Task Terminal_projection_retains_cached_active_revision_until_all_writes_finish(string pendingWrite)
    {
        using var fixture = new Fixture();
        var first = Snapshot();
        await fixture.ApplyAsync(first);
        var terminal = Next(first) with
        {
            State = Next(first).State with
            {
                Status = WorkflowStrategyMachineStatus.Completed,
                TerminalAtUtc = first.UpdatedAtUtc.AddSeconds(1)
            }
        };
        var gate = fixture.Hold(pendingWrite, 2);
        var projection = fixture.ApplyAsync(terminal);
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        fixture.Cache.TryGet(first.EntityId.Format(), out var cached).Should().BeTrue();
        cached!.WorkflowRevision.Should().Be(1);
        fixture.Notifications.Select(x => x.WorkflowRevision).Should().Equal(1);
        if (pendingWrite != "delete")
            fixture.Started.Should().NotContain(x => x.Kind == "delete",
                "active-store deletion must wait for terminal detail/history/index persistence");

        gate.Release.TrySetResult();
        await projection.WaitAsync(TimeSpan.FromSeconds(10));

        fixture.Cache.TryGet(first.EntityId.Format(), out _).Should().BeFalse();
        fixture.Notifications.Select(x => x.WorkflowRevision).Should().Equal(1, 2);
        fixture.UiNotifications.Select(x => x.WorkflowRevision).Should().Equal(1, 2);
    }

    [Fact]
    public async Task Same_entity_keeps_later_revision_behind_all_prior_writes_and_notification()
    {
        using var fixture = new Fixture();
        var first = Snapshot();
        var gate = fixture.Hold("timeline", 1);
        var prior = fixture.ApplyAsync(first);
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var later = fixture.ApplyAsync(Next(first));

        later.IsCompleted.Should().BeFalse();
        fixture.Started.Should().OnlyContain(x => x.Revision == 1);
        fixture.Notifications.Should().BeEmpty();

        gate.Release.TrySetResult();
        await Task.WhenAll(prior, later).WaitAsync(TimeSpan.FromSeconds(10));

        fixture.Notifications.Select(x => x.WorkflowRevision).Should().Equal(1, 2);
        fixture.UiNotifications.Select(x => x.WorkflowRevision).Should().Equal(1, 2);
        fixture.Cache.TryGet(first.EntityId.Format(), out var cached).Should().BeTrue();
        cached!.WorkflowRevision.Should().Be(2);
    }

    [Fact]
    public async Task A_failed_write_does_not_release_entity_lock_while_sibling_writes_are_pending()
    {
        using var fixture = new Fixture { FailedWrite = "detail" };
        var first = Snapshot();
        var gate = fixture.Hold("timeline", 1);
        var prior = fixture.ApplyAsync(first);
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var later = fixture.ApplyAsync(Next(first));

        prior.IsCompleted.Should().BeFalse();
        later.IsCompleted.Should().BeFalse();
        fixture.Started.Should().OnlyContain(x => x.Revision == 1);
        fixture.Notifications.Should().BeEmpty();
        fixture.UiNotifications.Should().BeEmpty();

        gate.Release.TrySetResult();
        var finish = async () => await Task.WhenAll(prior, later).WaitAsync(TimeSpan.FromSeconds(10));
        await finish.Should().ThrowAsync<InvalidOperationException>();
        fixture.Cache.TryGet(first.EntityId.Format(), out _).Should().BeFalse();
        fixture.Notifications.Should().BeEmpty();
    }

    [Fact]
    public async Task Different_entities_can_project_while_another_entity_write_is_pending()
    {
        using var fixture = new Fixture();
        var first = Snapshot();
        var gate = fixture.Hold("timeline", 1);
        var pending = fixture.ApplyAsync(first);
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        // Revision 2 selects a different controlled write gate; entity identity is also different.
        var other = Next(Snapshot());

        await fixture.ApplyAsync(other).WaitAsync(TimeSpan.FromSeconds(10));

        pending.IsCompleted.Should().BeFalse();
        fixture.Notifications.Should().ContainSingle().Which.EntityId.Should().Be(other.EntityId);
        fixture.UiNotifications.Should().ContainSingle().Which.EntityId.Should().Be(other.EntityId);
        gate.Release.TrySetResult();
        await pending.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Ui_notification_failure_does_not_fail_projected_workflow_dispatch()
    {
        using var fixture = new Fixture { FailUiNotification = true };

        await fixture.ApplyAsync(Snapshot()).WaitAsync(TimeSpan.FromSeconds(10));

        fixture.Notifications.Should().ContainSingle(
            "the authoritative realtime workflow dispatch remains successful");
        fixture.UiNotifications.Should().BeEmpty();
    }

    [Fact]
    public async Task Committed_event_runs_risk_and_subscription_handlers_without_a_database_scan()
    {
        using var fixture = new Fixture();
        var snapshot = Snapshot();

        await fixture.ApplyAsync(snapshot);

        fixture.Descriptor.UseDurableReplay.Should().BeTrue();
        await fixture.RiskProjection.Received(1)
            .ProjectCommittedAsync(snapshot, Arg.Any<CancellationToken>());
        await fixture.SubscriptionProjection.Received(1)
            .ProjectCommittedAsync(snapshot, Arg.Any<ProjectionExecutionContext>());
    }

    static WorkflowStrategyStateUpdatedEvent Snapshot()
    {
        var source = IntrinsicTimeStrategyWorkflowCommandStateTests.CreateStartedSnapshotForQualification();
        var entity = IntrinsicTimeStrategyWorkflowEntityId.Create(new FuturesItiSignalEntityId(
            $"TEST-{Guid.NewGuid():N}", new DateOnly(2026, 9, 9), TimeFrameType.Daily));
        var workflow = new StrategyWorkflowId(Guid.NewGuid());
        return source with
        {
            EntityId = entity, WorkflowId = workflow, EventId = 1, WorkflowRevision = 1,
            State = source.State with { EntityId = entity, WorkflowId = workflow, WorkflowRevision = 1 }
        };
    }

    static WorkflowStrategyStateUpdatedEvent Next(WorkflowStrategyStateUpdatedEvent source) => source with
    {
        EventId = source.EventId + 1,
        WorkflowRevision = source.WorkflowRevision + 1,
        PreviousStatus = WorkflowStrategyMachineStatus.Started,
        State = source.State with { WorkflowRevision = source.WorkflowRevision + 1 }
    };

    sealed class WriteGate
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    sealed class Fixture : IDisposable
    {
        readonly ConcurrentDictionary<(string Kind, long Revision), WriteGate> gates = new();
        readonly ConcurrentDictionary<string, long> entityRevisions = new();
        readonly IntrinsicTimeStrategyWorkflowEventProjector projector;
        readonly EventProjectionDescriptor descriptor;
        public EventProjectionDescriptor Descriptor => descriptor;
        public TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Realtime.IWorkflowRiskProjection RiskProjection { get; }
            = Substitute.For<TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Realtime.IWorkflowRiskProjection>();
        public TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Realtime.ICommittedCompositionSubscriptionProjector SubscriptionProjection { get; }
            = Substitute.For<TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Realtime.ICommittedCompositionSubscriptionProjector>();
        public IIntrinsicTimeStrategyWorkflowProjectionCache Cache { get; } = IntrinsicTimeStrategyWorkflowProjectionCache.Shared;
        public ConcurrentQueue<(string Kind, long Revision)> Started { get; } = new();
        public ConcurrentQueue<(string Kind, long Revision)> Completed { get; } = new();
        public ConcurrentQueue<WorkflowStrategyStateUpdatedEvent> Notifications { get; } = new();
        public ConcurrentQueue<IntrinsicTimeStrategyWorkflowUpdatedNotifyEvent> UiNotifications { get; } = new();
        public string? FailedWrite { get; init; }
        public bool FailUiNotification { get; init; }

        public Fixture()
        {
            var tradeDb = Substitute.For<ITradeDbContext>();
            tradeDb.InsertIntrinsicTimeStrategyWorkflowTimelineAsync(Arg.Any<IntrinsicTimeStrategyWorkflowTimelineReadModel>(), Arg.Any<CancellationToken>())
                .Returns(call => Write("timeline", call.ArgAt<IntrinsicTimeStrategyWorkflowTimelineReadModel>(0).WorkflowRevision));
            tradeDb.InsertIntrinsicTimeStrategyWorkflowStartAttemptAsync(Arg.Any<IntrinsicTimeStrategyWorkflowStartAttemptReadModel>(), Arg.Any<CancellationToken>())
                .Returns(call => Write("start", call.ArgAt<IntrinsicTimeStrategyWorkflowStartAttemptReadModel>(0).SourceEventId));
            tradeDb.UpsertIntrinsicTimeStrategyWorkflowAsync(Arg.Any<IntrinsicTimeStrategyWorkflowReadModel>(), Arg.Any<CancellationToken>())
                .Returns(call => Write("detail", call.ArgAt<IntrinsicTimeStrategyWorkflowReadModel>(0).WorkflowRevision));
            tradeDb.UpsertIntrinsicTimeStrategyWorkflowByEntityAsync(Arg.Any<IntrinsicTimeStrategyWorkflowHistoryReadModel>(), Arg.Any<CancellationToken>())
                .Returns(call => Write("entity", call.ArgAt<IntrinsicTimeStrategyWorkflowHistoryReadModel>(0).WorkflowRevision));
            tradeDb.UpsertIntrinsicTimeStrategyWorkflowByStatusDayAsync(Arg.Any<IntrinsicTimeStrategyWorkflowHistoryReadModel>(), Arg.Any<CancellationToken>())
                .Returns(call => Write("status", call.ArgAt<IntrinsicTimeStrategyWorkflowHistoryReadModel>(0).WorkflowRevision));
            tradeDb.UpsertActiveIntrinsicTimeStrategyWorkflowAsync(Arg.Any<ActiveIntrinsicTimeStrategyWorkflowReadModel>(), Arg.Any<CancellationToken>())
                .Returns(call => Write("active", call.ArgAt<ActiveIntrinsicTimeStrategyWorkflowReadModel>(0).WorkflowRevision));
            tradeDb.DeleteActiveIntrinsicTimeStrategyWorkflowAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(call => Write("delete", entityRevisions[call.ArgAt<string>(0)]));
            var dbFactory = Substitute.For<IDbContextFactory>();
            dbFactory.TradeDb.Returns(tradeDb);
            var context = Substitute.For<IIntrinsicTimeStrategyWorkflowCommandContext>();
            context.DbFactory.Returns(dbFactory);
            context.DbEventSource.Returns(Substitute.For<IEventSourceActorDbContext>());
            context.DurableReplayQueue.Returns(Substitute.For<IDurableReplayQueue>());
            context.BlackboardService.Returns(Substitute.For<IBlackboardService>());
            context.Logger.Returns(Substitute.For<ILogger<IntrinsicTimeStrategyWorkflowCommandActor>>());
            context.SendAsync<WorkflowStrategyStateUpdatedEvent, IntrinsicTimeStrategyWorkflowEntityId>(
                    Arg.Any<WorkflowStrategyStateUpdatedEvent>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    Notifications.Enqueue(call.ArgAt<WorkflowStrategyStateUpdatedEvent>(0));
                    return ValueTask.CompletedTask;
                });
            context.SendAsync<IntrinsicTimeStrategyWorkflowUpdatedNotifyEvent, IntrinsicTimeStrategyWorkflowEntityId>(
                    Arg.Any<IntrinsicTimeStrategyWorkflowUpdatedNotifyEvent>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    if (FailUiNotification)
                        throw new InvalidOperationException("UI notification unavailable");
                    UiNotifications.Enqueue(call.ArgAt<IntrinsicTimeStrategyWorkflowUpdatedNotifyEvent>(0));
                    return ValueTask.CompletedTask;
                });
            projector = new IntrinsicTimeStrategyWorkflowEventProjector(context,
                RiskProjection, SubscriptionProjection);
            descriptor = projector.ProjectionDescriptors.Single();
        }

        public WriteGate Hold(string kind, long revision)
        {
            var gate = new WriteGate();
            gates[(kind, revision)] = gate;
            return gate;
        }

        public async Task ApplyAsync(WorkflowStrategyStateUpdatedEvent snapshot)
        {
            entityRevisions[snapshot.EntityId.Format()] = snapshot.WorkflowRevision;
            var context = new ProjectionExecutionContext(projector.ProjectorName, snapshot.EventId, 42,
                new EventProjectorEffectIdentity(projector.ProjectorName, snapshot.EventId, EventProjectorEffectKind.TargetProjection),
                Guid.NewGuid(), EventProjectionIdempotencyStrategy.NaturalKeyMutation, CancellationToken.None,
                streamVersion: snapshot.WorkflowRevision);
            await descriptor.ApplyAsync(snapshot, context);
        }

        async Task Write(string kind, long revision)
        {
            Started.Enqueue((kind, revision));
            if (gates.TryGetValue((kind, revision), out var gate))
            {
                gate.Entered.TrySetResult();
                await gate.Release.Task.ConfigureAwait(false);
            }
            if (kind == FailedWrite) throw new InvalidOperationException("projection write failed");
            Completed.Enqueue((kind, revision));
        }

        public void Dispose()
        {
            foreach (var gate in gates.Values) gate.Release.TrySetResult();
            foreach (var entity in entityRevisions.Keys) Cache.Remove(entity);
        }
    }
}
