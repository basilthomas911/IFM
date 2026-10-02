using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Domain.Supervisor.Shared.Service.Lifecycle;
using TomasAI.IFM.Domain.Supervisor.Health.Query.Actor;
using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Supervisor.UnitTests;

public sealed class SupervisorBootstrapTests
{
    [Fact]
    public async Task Starts_and_stops_only_supervisor_actors()
    {
        var fixture = new Fixture();

        var started = await fixture.Bootstrap.StartSupervisorAsync(CancellationToken.None);
        var stopped = await fixture.Bootstrap.StopSupervisorAsync(CancellationToken.None);

        Assert.Equal(SupervisorOperationOutcome.Succeeded, started.Outcome);
        Assert.Equal(1, started.StartedActors);
        Assert.Equal(SupervisorOperationOutcome.Succeeded, stopped.Outcome);
        Assert.Equal(1, stopped.StoppedActors);
        Assert.Equal([
            "register:SupervisorQuery",
            "start:SupervisorQuery",
            "stop:SupervisorQuery",
            "remove:SupervisorQuery",
            "runtime:shutdown"
        ], fixture.Events);
        Assert.False(fixture.Bootstrap.IsRunning);
    }

    [Fact]
    public async Task Managed_descriptors_are_never_constructed_by_bootstrap()
    {
        var fixture = new Fixture();

        var result = await fixture.Bootstrap.StartSupervisorAsync(CancellationToken.None);

        Assert.True(result.Succeeded);
        fixture.Factory.DidNotReceive().GetActor(Fixture.ManagedDescriptor);
    }

    [Fact]
    public async Task Startup_failure_is_typed_and_rolls_back_without_throwing()
    {
        var fixture = new Fixture(failStartup: true);

        var result = await fixture.Bootstrap.StartSupervisorAsync(CancellationToken.None);

        Assert.Equal(SupervisorOperationOutcome.Failed, result.Outcome);
        Assert.Equal("StartSupervisor", result.Stage);
        Assert.False(fixture.Bootstrap.IsRunning);
        Assert.Contains("remove:SupervisorQuery", fixture.Events);
    }

    [Fact]
    public async Task Duplicate_start_and_stop_are_idempotent()
    {
        var fixture = new Fixture();

        var firstStart = await fixture.Bootstrap.StartSupervisorAsync(CancellationToken.None);
        var secondStart = await fixture.Bootstrap.StartSupervisorAsync(CancellationToken.None);
        var firstStop = await fixture.Bootstrap.StopSupervisorAsync(CancellationToken.None);
        var secondStop = await fixture.Bootstrap.StopSupervisorAsync(CancellationToken.None);

        Assert.True(firstStart.Succeeded);
        Assert.Equal("AlreadyRunning", secondStart.Stage);
        Assert.True(firstStop.Succeeded);
        Assert.Equal("AlreadyStopped", secondStop.Stage);
        Assert.Equal(1, fixture.Events.Count(value => value == "start:SupervisorQuery"));
        Assert.Equal(1, fixture.Events.Count(value => value == "stop:SupervisorQuery"));
    }

    [Fact]
    public async Task Bootstrap_result_preserves_primary_and_rollback_failures()
    {
        var fixture = new Fixture(failStartup: true, failRemoval: true);

        var result = await fixture.Bootstrap.StartSupervisorAsync(CancellationToken.None);

        Assert.Equal(SupervisorOperationOutcome.Failed, result.Outcome);
        Assert.Contains("injected bootstrap failure", result.FailureReason, StringComparison.Ordinal);
        Assert.Contains("Cleanup:", result.FailureReason, StringComparison.Ordinal);
        Assert.Contains("injected bootstrap removal failure", result.FailureReason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Hanging_supervisor_actor_is_bounded_and_retained_for_retry()
    {
        var fixture = new Fixture(blockStop: true, shutdownTimeout: TimeSpan.FromMilliseconds(25));
        Assert.True((await fixture.Bootstrap.StartSupervisorAsync(CancellationToken.None)).Succeeded);

        var first = await fixture.Bootstrap.StopSupervisorAsync(CancellationToken.None);
        Assert.True(fixture.Bootstrap.IsRunning);
        fixture.ReleaseStop.TrySetResult();
        var second = await fixture.Bootstrap.StopSupervisorAsync(CancellationToken.None);

        Assert.Equal(SupervisorOperationOutcome.PartiallyCompleted, first.Outcome);
        Assert.Equal("HostRecoveryRequired", first.Stage);
        Assert.Contains("timed out", first.FailureReason, StringComparison.Ordinal);
        Assert.True(second.Succeeded);
        Assert.False(fixture.Bootstrap.IsRunning);
        Assert.Equal(1, fixture.Events.Count(value => value == "remove:SupervisorQuery"));
        Assert.Equal(1, fixture.Events.Count(value => value == "runtime:shutdown"));
    }

    [Fact]
    public async Task Hanging_runtime_shutdown_is_bounded_and_retried_without_restopping_actor()
    {
        var fixture = new Fixture(blockRuntimeShutdown: true, shutdownTimeout: TimeSpan.FromMilliseconds(25));
        Assert.True((await fixture.Bootstrap.StartSupervisorAsync(CancellationToken.None)).Succeeded);

        var first = await fixture.Bootstrap.StopSupervisorAsync(CancellationToken.None);
        Assert.True(fixture.Bootstrap.IsRunning);
        fixture.ReleaseRuntimeShutdown.TrySetResult();
        var second = await fixture.Bootstrap.StopSupervisorAsync(CancellationToken.None);

        Assert.Equal(SupervisorOperationOutcome.PartiallyCompleted, first.Outcome);
        Assert.Equal("HostRecoveryRequired", first.Stage);
        Assert.Contains("Runtime:", first.FailureReason, StringComparison.Ordinal);
        Assert.True(second.Succeeded);
        Assert.Equal(1, fixture.Events.Count(value => value == "stop:SupervisorQuery"));
        Assert.Equal(1, fixture.Events.Count(value => value == "remove:SupervisorQuery"));
        Assert.Equal(2, fixture.Events.Count(value => value == "runtime:shutdown"));
    }

    sealed class Fixture
    {
        internal static readonly Type ManagedDescriptor = typeof(IActor<ManagedActor>);
        static readonly Type SupervisorDescriptor = typeof(IActor<SupervisorQueryActor>);
        readonly ActorMailboxId _supervisorId = new(ActorType.Query, "SupervisorQuery");
        readonly HashSet<ActorMailboxId> _running = [];

        public Fixture(
            bool failStartup = false,
            bool failRemoval = false,
            bool blockStop = false,
            bool blockRuntimeShutdown = false,
            TimeSpan? shutdownTimeout = null)
        {
            var supervisorActor = Substitute.For<IActor>();
            supervisorActor.Id.Returns(_supervisorId);
            supervisorActor.IsRunning.Returns(_ => _running.Contains(_supervisorId));
            var registry = Substitute.For<IActorRegistry>();
            registry.ActorTypes.Returns([ManagedDescriptor, SupervisorDescriptor]);
            Factory = Substitute.For<IActorFactory>();
            Factory.GetActor(SupervisorDescriptor).Returns(supervisorActor);

            var container = Substitute.For<IContainerInstance>();
            container.Resolve<IActorProducer>().Returns(Substitute.For<IActorProducer>());
            container.Resolve<IActorConsumer>().Returns(Substitute.For<IActorConsumer>());
            var supervisor = Substitute.For<IActorSupervisor>();
            supervisor.Container.Returns(container);
            supervisor.When(value => value.AddActor(Arg.Any<IActor>()))
                .Do(call => Events.Add("register:" + call.Arg<IActor>().Id.Name));
            supervisor.When(value => value.RemoveActor(Arg.Any<IActor>()))
                .Do(call =>
                {
                    Events.Add("remove:" + call.Arg<IActor>().Id.Name);
                    if (failRemoval)
                        throw new InvalidOperationException("injected bootstrap removal failure");
                });
            supervisor.StartAsync(_supervisorId, Arg.Any<CancellationToken>()).Returns(_ =>
            {
                Events.Add("start:SupervisorQuery");
                if (failStartup)
                    return ValueTask.FromException(new InvalidOperationException("injected bootstrap failure"));
                _running.Add(_supervisorId);
                return ValueTask.CompletedTask;
            });
            supervisor.StopAsync(_supervisorId, Arg.Any<CancellationToken>()).Returns(_ =>
            {
                Events.Add("stop:SupervisorQuery");
                if (blockStop && !ReleaseStop.Task.IsCompleted)
                    return new ValueTask(ReleaseStop.Task);
                _running.Remove(_supervisorId);
                return ValueTask.CompletedTask;
            });
            supervisor.ShutdownAsync(Arg.Any<CancellationToken>()).Returns(_ =>
            {
                Events.Add("runtime:shutdown");
                if (blockRuntimeShutdown && !ReleaseRuntimeShutdown.Task.IsCompleted)
                    return new ValueTask(ReleaseRuntimeShutdown.Task);
                return ValueTask.CompletedTask;
            });

            Bootstrap = new(
                supervisor,
                registry,
                Factory,
                new ActorRuntimeStartupOptions
                {
                    MaximumConcurrency = 1,
                    ActorShutdownTimeout = shutdownTimeout ?? TimeSpan.FromSeconds(5)
                },
                NullLogger<SupervisorBootstrap>.Instance);
        }

        public List<string> Events { get; } = [];
        public TaskCompletionSource ReleaseStop { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseRuntimeShutdown { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IActorFactory Factory { get; }
        public SupervisorBootstrap Bootstrap { get; }
    }

    sealed class ManagedActor : IActor<ManagedActor>
    {
        public ActorMailboxId Id => throw new NotSupportedException();
        public IActorMailbox Mailbox => throw new NotSupportedException();
        public bool IsRunning => throw new NotSupportedException();
        public ValueTask HandleMessageAsync(IActorMessage message) => throw new NotSupportedException();
        public ValueTask StartAsync(IActorSupervisor supervisor) => throw new NotSupportedException();
        public ValueTask StopAsync() => throw new NotSupportedException();
    }
}
