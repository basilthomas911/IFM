using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Domain.Supervisor.Health;
using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.ReadModels;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Supervisor.UnitTests;

public sealed class SupervisorHistoryStoreTests
{
    [Fact]
    public async Task Observation_is_queryable_immediately_and_persisted_off_the_caller_thread()
    {
        var persistence = new Persistence();
        using var history = new SupervisorHistoryStore(
            new SupervisorOperationStore(), persistence, NullLogger<SupervisorHistoryStore>.Instance);
        var coordinator = new SupervisorHealthActionCoordinator(
            Substitute.For<IActorSupervisor>(), Substitute.For<IActorRuntimeMetricsSourceProvider>(),
            new SupervisorOperationStore(), new SupervisorIncidentStore(), history,
            new SupervisorHealthActionOptions { AutomaticMutationEnabled = false },
            NullLogger<SupervisorHealthActionCoordinator>.Instance);
        var now = DateTime.UtcNow;
        var snapshot = new SupervisorActorMetricsSnapshot(
            42, now, 1, 2, TimeSpan.Zero, now.AddMinutes(1),
            1, 1, 0, 1, 0, 0, 0, 0, 0, 0, 0, SupervisorSnapshotQuality.Complete, []);

        coordinator.Observe(snapshot);

        Assert.Equal(42, Assert.Single(history.Read(now.AddSeconds(-1), now.AddSeconds(1))).Revision);
        Assert.Equal(42, (await persistence.Written.Task.WaitAsync(TimeSpan.FromSeconds(5))).Revision);
    }

    [Fact]
    public async Task Observe_only_mode_records_health_without_restarting_the_mailbox()
    {
        var supervisor = Substitute.For<IActorSupervisor>();
        var operations = new SupervisorOperationStore();
        using var history = new SupervisorHistoryStore(
            operations, new Persistence(), NullLogger<SupervisorHistoryStore>.Instance);
        var coordinator = new SupervisorHealthActionCoordinator(
            supervisor, Substitute.For<IActorRuntimeMetricsSourceProvider>(), operations,
            new SupervisorIncidentStore(), history,
            new SupervisorHealthActionOptions { AutomaticMutationEnabled = false },
            NullLogger<SupervisorHealthActionCoordinator>.Instance);

        coordinator.Observe(CreateRestartRequiredSnapshot());
        await Task.Delay(100);

        Assert.Empty(operations.RecentOperations);
        await supervisor.DidNotReceiveWithAnyArgs().RestartAsync(default, default, default, default);
    }

    [Fact]
    public async Task Enabled_mode_generation_fences_and_audits_automatic_restart()
    {
        var snapshot = CreateRestartRequiredSnapshot();
        var thread = snapshot.Actors[0].Threads[0];
        var runtimeSnapshot = new SupervisorActorSnapshot(
            snapshot.Actors[0].ActorId, "Test", "TestActor", true,
            SupervisorActorLifecycleState.Running, thread.Generation,
            SupervisorActorHealthStatus.Red, thread.QueueDepth, 1, 1, 0, 0, 0, 0, 0,
            [new ActorMailboxMetricsSnapshot(
                thread.ThreadId, thread.QueueDepth, thread.QueueCapacity, thread.PeakQueueDepth,
                1, 1, 0, 0, 0, 0, 0, true, ActorMailboxLifecycleState.Running,
                thread.Generation, false, "", null, null, null, null, "", "", 1)]);
        var source = Substitute.For<IActorRuntimeMetricsSource>();
        source.ActorId.Returns(snapshot.Actors[0].ActorId);
        source.CaptureSnapshot().Returns(runtimeSnapshot);
        var sources = Substitute.For<IActorRuntimeMetricsSourceProvider>();
        sources.CaptureActorMetricsSources().Returns([source]);
        var supervisor = Substitute.For<IActorSupervisor>();
        supervisor.RestartAsync(thread.ThreadId, thread.Generation, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(true));
        var operations = new SupervisorOperationStore();
        using var history = new SupervisorHistoryStore(
            operations, new Persistence(), NullLogger<SupervisorHistoryStore>.Instance);
        var coordinator = new SupervisorHealthActionCoordinator(
            supervisor, sources, operations, new SupervisorIncidentStore(), history,
            new SupervisorHealthActionOptions { AutomaticMutationEnabled = true },
            NullLogger<SupervisorHealthActionCoordinator>.Instance);

        coordinator.Observe(snapshot);
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (operations.RecentOperations.Count == 0 && DateTime.UtcNow < deadline)
            await Task.Delay(10);

        var operation = Assert.Single(operations.RecentOperations);
        Assert.Equal(SupervisorOperationOutcome.Succeeded, operation.Outcome);
        await supervisor.Received(1).RestartAsync(
            thread.ThreadId, thread.Generation, TimeSpan.FromMinutes(1), Arg.Any<CancellationToken>());
    }

    static SupervisorActorMetricsSnapshot CreateRestartRequiredSnapshot()
    {
        var now = DateTime.UtcNow;
        var actorId = new ActorMailboxId(ActorType.Command, "QualificationActor");
        var threadId = new ActorThreadId(ActorType.Command, "QualificationActor", "entity-1");
        var thread = new SupervisorActorThreadMetrics(
            threadId, 64, 64, 64, TimeSpan.FromMinutes(15), 64, 0, 0, 0, 0, 0,
            "Process", "Receive", TimeSpan.FromMinutes(15), SupervisorActorHealth.Critical,
            TimeSpan.FromMinutes(15), true, 7, SupervisorMailboxPressureState.Critical,
            1, 0, now.AddMinutes(-15), false, true, true);
        var actor = new SupervisorActorMetrics(
            actorId, 7, true, SupervisorActorHealth.Critical, 1, 64, 0, 0,
            SupervisorSnapshotQuality.Complete, [thread]);
        return new SupervisorActorMetricsSnapshot(
            1, now, 1, 2, TimeSpan.Zero, now.AddMinutes(1),
            1, 1, 0, 0, 0, 1, 0, 1, 64, 0, 0,
            SupervisorSnapshotQuality.Complete, [actor]);
    }

    sealed class Persistence : ISupervisorHistoryPersistence
    {
        public TaskCompletionSource<SupervisorHealthHistoryPoint> Written { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask AppendAsync(SupervisorHealthHistoryPoint point, CancellationToken cancellationToken)
        {
            Written.TrySetResult(point);
            return ValueTask.CompletedTask;
        }

        public ValueTask<IReadOnlyList<SupervisorHealthHistoryPoint>> ReadAsync(
            DateTime fromUtc, DateTime toUtc, int maximumCount, CancellationToken cancellationToken)
            => ValueTask.FromResult<IReadOnlyList<SupervisorHealthHistoryPoint>>([]);
    }
}
