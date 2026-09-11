using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using Xunit;

namespace TomasAI.IFM.Shared.UnitTests.EventModelActor;

public sealed class SupervisorRuntimeContextTests
{
    [Fact]
    public async Task SupervisorSnapshot_ListsRegisteredActorWithoutUsingItsMailbox()
    {
        var container = Mock.Of<IContainerInstance>();
        await using var supervisor = new ActorSupervisor(container, NullLogger<ActorSupervisor>.Instance);
        var actor = new Mock<IActor>();
        var id = new ActorMailboxId(ActorType.Event, "ProjectionStatus");
        var mailbox = new Mock<IActorMailbox>();
        mailbox.SetupGet(value => value.Metrics).Returns(new ActorMetricsStore(id));
        actor.SetupGet(value => value.Id).Returns(id);
        actor.SetupGet(value => value.Mailbox).Returns(mailbox.Object);
        actor.SetupGet(value => value.IsRunning).Returns(true);

        supervisor.AddActor(actor.Object);
        var snapshot = supervisor.RuntimeContext.CaptureSnapshot();

        snapshot.ActorCount.Should().Be(1);
        snapshot.RunningActorCount.Should().Be(1);
        snapshot.OverallStatus.Should().Be(SupervisorActorHealthStatus.Green);
        snapshot.Actors.Single().ActorId.Should().Be(id);
        snapshot.Workers.Should().NotBeNullOrEmpty();
        snapshot.Workers!.Should().OnlyContain(worker => worker.IsStarted && !worker.IsFaulted);
        actor.Verify(value => value.HandleMessageAsync(It.IsAny<IActorMessage>()), Times.Never);
    }

    [Fact]
    public void RemovedDenormalizerContracts_AreAbsentFromSharedAssembly()
    {
        var assembly = typeof(ActorSupervisor).Assembly;
        assembly.GetType("TomasAI.IFM.Shared.EventModelActor.BaseDenormalizerActor`1").Should().BeNull();
        assembly.GetType("TomasAI.IFM.Shared.EventModelActor.DenormalizerActorContext").Should().BeNull();
        assembly.GetTypes().Select(type => type.Name).Should().NotContain(name => name.Contains("DenormalizerActor"));
    }

    [Fact]
    public async Task ConcurrentLifecycleCalls_AreSerializedAndGenerationFenced()
    {
        await using var supervisor = new ActorSupervisor(
            Mock.Of<IContainerInstance>(), NullLogger<ActorSupervisor>.Instance);
        var actor = new LifecycleActor();
        supervisor.AddActor(actor);

        await Task.WhenAll(
            supervisor.StartAsync(actor.Id).AsTask(),
            supervisor.StartAsync(actor.Id).AsTask());

        actor.StartCount.Should().Be(1);
        var started = supervisor.RuntimeContext.CaptureSnapshot().Actors.Single();
        started.LifecycleState.Should().Be(SupervisorActorLifecycleState.Running);
        started.Generation.Should().Be(1);

        await supervisor.RestartAsync(actor.Id);
        actor.StartCount.Should().Be(2);
        actor.StopCount.Should().Be(1);
        supervisor.RuntimeContext.CaptureSnapshot().Actors.Single().Generation.Should().Be(2);

        await Task.WhenAll(
            supervisor.StopAsync(actor.Id).AsTask(),
            supervisor.StopAsync(actor.Id).AsTask());
        actor.StopCount.Should().Be(2);
        supervisor.RuntimeContext.CaptureSnapshot().Actors.Single().LifecycleState
            .Should().Be(SupervisorActorLifecycleState.Stopped);
    }

    [Fact]
    public void FailureHistory_PreservesPrimaryAndSecondaryEvidenceWithinDateRange()
    {
        var runtime = new SupervisorRuntimeContext();
        var threadId = new ActorThreadId(ActorType.Query, "HealthQuery", "request-1");
        var primaryId = runtime.RecordFailure(
            threadId.MailboxId, threadId, "Read", ActorFailureStage.Execution,
            new InvalidOperationException("primary"));
        var secondaryId = runtime.RecordFailure(
            threadId.MailboxId, threadId, "Read", ActorFailureStage.ExceptionHandling,
            new IOException("secondary"), primaryId);

        var snapshot = runtime.CaptureSnapshot(DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));

        snapshot.Failures.Should().HaveCount(2);
        snapshot.Failures.Single(failure => failure.FailureId == secondaryId).PrimaryFailureId
            .Should().Be(primaryId);
        snapshot.Failures.Single(failure => failure.FailureId == primaryId).Error.Should().Be("primary");
    }

    [Fact]
    public void ProjectorSource_ExposesDurableQueueNamesAndRecoveryReadiness()
    {
        var runtime = new SupervisorRuntimeContext();
        runtime.RegisterProjector(new ProjectorSource());

        var projector = runtime.CaptureSnapshot().Projectors.Single();

        projector.ProjectorName.Should().Be("Orders");
        projector.DurableProcessQueue.Should().Be("Orders.ProcessQueue");
        projector.DurableReplayQueue.Should().Be("Orders.ReplayQueue");
        projector.RecoveryEventsQueued.Should().Be(3);
        projector.IsReady.Should().BeTrue();
    }

    [Fact]
    public void ActorOwnedMetricUpdates_AllocateNothingAfterMailboxRegistration()
    {
        var id = new ActorThreadId(ActorType.Realtime, "Price", "ES");
        using var queue = new ActorThreadQueueV2(8);
        queue.SetId(id);
        queue.Start();
        var store = new ActorMetricsStore(id.MailboxId);
        var metrics = store.RegisterMailbox(id, queue);
        metrics.RecordAccepted();
        metrics.RecordDequeued("Update");
        metrics.RecordSucceeded();

        // Cross tiered-JIT/PGO thresholds before measuring the steady-state path.
        for (var index = 0; index < 100_000; index++)
        {
            metrics.RecordAccepted();
            metrics.RecordDequeued("Update");
            metrics.RecordSucceeded();
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 10_000; index++)
        {
            metrics.RecordAccepted();
            metrics.RecordDequeued("Update");
            metrics.RecordSucceeded();
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.Should().Be(0);
    }

    [Fact]
    public async Task LifecycleGuard_PreservesPrimaryFailureAndLinksCleanupFailure()
    {
        var runtime = new SupervisorRuntimeContext();
        var actorId = new ActorMailboxId(ActorType.Event, "LifecycleEvidence");

        var action = () => ActorLifecycleGuard.StopAsync(
            runtime,
            actorId,
            () => ValueTask.FromException(new IOException("transport stop failed")),
            () => ValueTask.FromException(new InvalidOperationException("actor cleanup failed")),
            CancellationToken.None).AsTask();

        await action.Should().ThrowAsync<IOException>().WithMessage("transport stop failed");
        var failures = runtime.CaptureSnapshot().Failures;
        failures.Should().HaveCount(2);
        var primary = failures.Single(failure => failure.Stage == ActorFailureStage.Shutdown);
        failures.Single(failure => failure.Stage == ActorFailureStage.Cleanup).PrimaryFailureId
            .Should().Be(primary.FailureId);
        primary.ExceptionDetail.Should().Contain("transport stop failed");
    }

    [Fact]
    public async Task LifecycleGuard_DoesNotRecordExpectedCancellation()
    {
        var runtime = new SupervisorRuntimeContext();
        var actorId = new ActorMailboxId(ActorType.Query, "LifecycleCancellation");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var action = () => ActorLifecycleGuard.StopAsync(
            runtime,
            actorId,
            () => ValueTask.FromCanceled(cancellation.Token),
            () => ValueTask.CompletedTask,
            cancellation.Token).AsTask();

        await action.Should().ThrowAsync<OperationCanceledException>();
        runtime.CaptureSnapshot().Failures.Should().BeEmpty();
    }

    sealed class LifecycleActor : IActor
    {
        int _running;
        int _starts;
        int _stops;
        readonly IActorMailbox _mailbox;

        internal LifecycleActor()
        {
            var mailbox = new Mock<IActorMailbox>();
            var queues = new Mock<IActorThreadQueues>();
            queues.Setup(value => value.WaitForIdleAsync(
                    It.IsAny<System.TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            mailbox.SetupGet(value => value.Metrics).Returns(new ActorMetricsStore(Id));
            mailbox.SetupGet(value => value.ThreadQueues).Returns(queues.Object);
            _mailbox = mailbox.Object;
        }

        public ActorMailboxId Id { get; } = new(ActorType.Command, "LifecycleTest");
        public IActorMailbox Mailbox => _mailbox;
        public bool IsRunning => Volatile.Read(ref _running) != 0;
        internal int StartCount => Volatile.Read(ref _starts);
        internal int StopCount => Volatile.Read(ref _stops);
        public ValueTask HandleMessageAsync(IActorMessage message) => ValueTask.CompletedTask;
        public ValueTask StartAsync(IActorSupervisor supervisor)
        {
            Interlocked.Increment(ref _starts);
            Volatile.Write(ref _running, 1);
            return ValueTask.CompletedTask;
        }
        public ValueTask StopAsync()
        {
            Interlocked.Increment(ref _stops);
            Volatile.Write(ref _running, 0);
            return ValueTask.CompletedTask;
        }
    }

    sealed class ProjectorSource : ISupervisorProjectorMetricsSource
    {
        public string SupervisorProjectorKey => "Trade:Orders";
        public SupervisorProjectorSnapshot CaptureSupervisorSnapshot() => new(
            "Trade", "Orders", "Orders.ProcessQueue", "Orders.ReplayQueue", true,
            4, 3, DateTime.UtcNow, string.Empty);
    }
}
