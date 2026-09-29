using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Domain.Supervisor.Lifecycle;
using TomasAI.IFM.Domain.Supervisor.Query.Actor;
using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Supervisor.UnitTests;

public sealed class SupervisorManagedActorLifecycleTests
{
    [Fact]
    public async Task Starts_only_managed_actors_then_opens_intake_and_starts_poller()
    {
        var fixture = new Fixture();

        var result = await fixture.Lifecycle.StartupActorsAsync(CancellationToken.None);

        Assert.Equal(SupervisorOperationOutcome.Succeeded, result.Outcome);
        Assert.Equal(1, result.StartedActors);
        Assert.DoesNotContain("start:SupervisorQuery", fixture.Events);
        Assert.True(fixture.Events.IndexOf("start:ManagedQuery") < fixture.Events.IndexOf("intake:start"));
        Assert.True(fixture.Events.IndexOf("intake:start") < fixture.Events.IndexOf("poller:start"));
        Assert.True(fixture.Ready);
    }

    [Fact]
    public async Task Stops_poller_and_intake_before_only_managed_actors()
    {
        var fixture = new Fixture();
        Assert.True((await fixture.Lifecycle.StartupActorsAsync(CancellationToken.None)).Succeeded);
        fixture.Events.Clear();

        var result = await fixture.Lifecycle.ShutdownActorsAsync(CancellationToken.None);

        Assert.Equal(SupervisorOperationOutcome.Succeeded, result.Outcome);
        Assert.Equal(1, result.StoppedActors);
        Assert.True(fixture.Events.IndexOf("poller:stop") < fixture.Events.IndexOf("intake:stop"));
        Assert.True(fixture.Events.IndexOf("intake:stop") < fixture.Events.IndexOf("stop:ManagedQuery"));
        Assert.DoesNotContain("stop:SupervisorQuery", fixture.Events);
        Assert.False(fixture.Ready);
    }

    [Fact]
    public async Task Startup_failure_returns_typed_result_closes_readiness_and_rolls_back()
    {
        var fixture = new Fixture(failManagedStartup: true);

        var result = await fixture.Lifecycle.StartupActorsAsync(CancellationToken.None);

        Assert.Equal(SupervisorOperationOutcome.Failed, result.Outcome);
        Assert.Equal("StartManaged", result.Stage);
        Assert.False(fixture.Ready);
        Assert.DoesNotContain("runtime:shutdown", fixture.Events);
        Assert.DoesNotContain("stop:SupervisorQuery", fixture.Events);
        Assert.DoesNotContain("poller:start", fixture.Events);
    }

    [Fact]
    public async Task Cancelled_startup_returns_cancelled_without_throwing()
    {
        var fixture = new Fixture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = await fixture.Lifecycle.StartupActorsAsync(cancellation.Token);

        Assert.Equal(SupervisorOperationOutcome.Cancelled, result.Outcome);
        Assert.False(fixture.Ready);
    }

    [Fact]
    public async Task Managed_startup_rejects_a_missing_supervisor_boundary()
    {
        var fixture = new Fixture(bootstrapRunning: false);

        var result = await fixture.Lifecycle.StartupActorsAsync(CancellationToken.None);

        Assert.Equal(SupervisorOperationOutcome.Failed, result.Outcome);
        Assert.Equal("Discover", result.Stage);
        Assert.DoesNotContain(fixture.Events, value => value.StartsWith("start:", StringComparison.Ordinal));
        Assert.False(fixture.Ready);
    }

    [Fact]
    public async Task Duplicate_startup_and_shutdown_are_idempotent()
    {
        var fixture = new Fixture();

        var firstStart = await fixture.Lifecycle.StartupActorsAsync(CancellationToken.None);
        var secondStart = await fixture.Lifecycle.StartupActorsAsync(CancellationToken.None);
        var firstStop = await fixture.Lifecycle.ShutdownActorsAsync(CancellationToken.None);
        var secondStop = await fixture.Lifecycle.ShutdownActorsAsync(CancellationToken.None);

        Assert.True(firstStart.Succeeded);
        Assert.Equal("AlreadyRunning", secondStart.Stage);
        Assert.True(firstStop.Succeeded);
        Assert.Equal("AlreadyStopped", secondStop.Stage);
        Assert.Equal(1, fixture.Events.Count(value => value == "start:ManagedQuery"));
        Assert.Equal(1, fixture.Events.Count(value => value == "stop:ManagedQuery"));
    }

    [Fact]
    public async Task Poller_stop_timeout_returns_partial_result_and_keeps_readiness_closed()
    {
        var fixture = new Fixture(pollerStops: false);
        Assert.True((await fixture.Lifecycle.StartupActorsAsync(CancellationToken.None)).Succeeded);

        var result = await fixture.Lifecycle.ShutdownActorsAsync(CancellationToken.None);

        Assert.Equal(SupervisorOperationOutcome.PartiallyCompleted, result.Outcome);
        Assert.Equal("HostRecoveryRequired", result.Stage);
        Assert.False(fixture.Ready);
    }

    [Fact]
    public async Task Actor_stop_failure_returns_partial_result_without_stopping_supervisor()
    {
        var fixture = new Fixture(failManagedStop: true);
        Assert.True((await fixture.Lifecycle.StartupActorsAsync(CancellationToken.None)).Succeeded);

        var result = await fixture.Lifecycle.ShutdownActorsAsync(CancellationToken.None);

        Assert.Equal(SupervisorOperationOutcome.PartiallyCompleted, result.Outcome);
        Assert.Equal("HostRecoveryRequired", result.Stage);
        Assert.DoesNotContain("stop:SupervisorQuery", fixture.Events);
        Assert.False(fixture.Ready);
    }

    [Fact]
    public async Task Hanging_actor_is_bounded_and_retained_for_shutdown_retry()
    {
        var fixture = new Fixture(blockManagedStop: true, actorShutdownTimeout: TimeSpan.FromMilliseconds(25));
        Assert.True((await fixture.Lifecycle.StartupActorsAsync(CancellationToken.None)).Succeeded);

        var first = await fixture.Lifecycle.ShutdownActorsAsync(CancellationToken.None);
        fixture.ReleaseManagedStop.TrySetResult();
        var second = await fixture.Lifecycle.ShutdownActorsAsync(CancellationToken.None);

        Assert.Equal(SupervisorOperationOutcome.PartiallyCompleted, first.Outcome);
        Assert.Equal("HostRecoveryRequired", first.Stage);
        Assert.Contains("timed out", first.FailureReason, StringComparison.Ordinal);
        Assert.True(second.Succeeded);
        Assert.Equal(1, second.ExpectedActors);
        Assert.Equal(1, second.StoppedActors);
    }

    [Fact]
    public async Task Startup_result_preserves_primary_and_rollback_failures()
    {
        var fixture = new Fixture(failManagedStartup: true, failActorRemoval: true);

        var result = await fixture.Lifecycle.StartupActorsAsync(CancellationToken.None);

        Assert.Equal(SupervisorOperationOutcome.Failed, result.Outcome);
        Assert.Contains("injected startup failure", result.FailureReason, StringComparison.Ordinal);
        Assert.Contains("Cleanup:", result.FailureReason, StringComparison.Ordinal);
        Assert.Contains("injected removal failure", result.FailureReason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancelled_waiter_cannot_rollback_an_active_startup()
    {
        var fixture = new Fixture(blockManagedStart: true);
        var active = fixture.Lifecycle.StartupActorsAsync(CancellationToken.None).AsTask();
        await fixture.ManagedStartEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var cancelled = await fixture.Lifecycle.StartupActorsAsync(cancellation.Token);
        fixture.ReleaseManagedStart.TrySetResult();
        var completed = await active.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(SupervisorOperationOutcome.Cancelled, cancelled.Outcome);
        Assert.True(completed.Succeeded);
        Assert.True(fixture.Ready);
        Assert.DoesNotContain("remove:ManagedQuery", fixture.Events);
    }

    [Fact]
    public async Task Concurrent_startup_serializes_and_returns_one_already_running_result()
    {
        var fixture = new Fixture(blockManagedStart: true);
        var first = fixture.Lifecycle.StartupActorsAsync(CancellationToken.None).AsTask();
        await fixture.ManagedStartEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = fixture.Lifecycle.StartupActorsAsync(CancellationToken.None).AsTask();

        fixture.ReleaseManagedStart.TrySetResult();
        var results = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.All(results, result => Assert.True(result.Succeeded));
        Assert.Single(results, result => result.Stage == "Completed");
        Assert.Single(results, result => result.Stage == "AlreadyRunning");
        Assert.Equal(1, fixture.Events.Count(value => value == "start:ManagedQuery"));
    }

    [Fact]
    public async Task Shutdown_requested_during_startup_waits_then_performs_ordered_teardown()
    {
        var fixture = new Fixture(blockManagedStart: true);
        var startup = fixture.Lifecycle.StartupActorsAsync(CancellationToken.None).AsTask();
        await fixture.ManagedStartEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var shutdown = fixture.Lifecycle.ShutdownActorsAsync(CancellationToken.None).AsTask();

        Assert.False(shutdown.IsCompleted);
        fixture.ReleaseManagedStart.TrySetResult();
        var startupResult = await startup.WaitAsync(TimeSpan.FromSeconds(5));
        var shutdownResult = await shutdown.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(startupResult.Succeeded);
        Assert.True(shutdownResult.Succeeded);
        Assert.True(fixture.Events.IndexOf("poller:start") < fixture.Events.LastIndexOf("poller:stop"));
        Assert.True(fixture.Events.IndexOf("intake:start") < fixture.Events.LastIndexOf("intake:stop"));
        Assert.True(fixture.Events.IndexOf("start:ManagedQuery") < fixture.Events.LastIndexOf("stop:ManagedQuery"));
        Assert.False(fixture.Ready);
    }

    [Fact]
    public async Task Repeated_startup_shutdown_cycles_do_not_duplicate_managed_state()
    {
        var fixture = new Fixture();

        for (var cycle = 0; cycle < 3; cycle++)
        {
            Assert.True((await fixture.Lifecycle.StartupActorsAsync(CancellationToken.None)).Succeeded);
            Assert.True((await fixture.Lifecycle.ShutdownActorsAsync(CancellationToken.None)).Succeeded);
        }

        Assert.Equal(3, fixture.Events.Count(value => value == "start:ManagedQuery"));
        Assert.Equal(3, fixture.Events.Count(value => value == "stop:ManagedQuery"));
        Assert.Equal(3, fixture.Events.Count(value => value == "remove:ManagedQuery"));
        Assert.False(fixture.Ready);
    }

    [Fact]
    public async Task Restart_operation_is_generation_fenced_and_returns_typed_success()
    {
        var fixture = new Fixture();
        var target = new ActorThreadId(ActorType.Query, "ManagedQuery", "entity-1");
        fixture.ActorSupervisor.RestartAsync(target, 7, TimeSpan.FromSeconds(10), Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await fixture.Lifecycle.ExecuteAsync(new(
            Guid.NewGuid(), target, 7, SupervisorActorOperationKind.Restart,
            "operator", "Manual recovery", TimeSpan.FromSeconds(10)), CancellationToken.None);

        Assert.True(result.Succeeded);
        await fixture.ActorSupervisor.Received(1)
            .RestartAsync(target, 7, TimeSpan.FromSeconds(10), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unsafe_or_unbounded_operation_request_is_rejected_without_runtime_mutation()
    {
        var fixture = new Fixture();
        var result = await fixture.Lifecycle.ExecuteAsync(new(
            Guid.NewGuid(), new(ActorType.Query, "ManagedQuery", "entity-1"), 1,
            SupervisorActorOperationKind.Restart, new string('x', 129), "reason",
            TimeSpan.FromMinutes(11)), CancellationToken.None);

        Assert.Equal(SupervisorOperationOutcome.Rejected, result.Outcome);
        await fixture.ActorSupervisor.DidNotReceiveWithAnyArgs()
            .RestartAsync(default, default, default, default);
    }

    sealed class Fixture
    {
        static readonly Type SupervisorDescriptor = typeof(IActor<SupervisorQueryActor>);
        static readonly Type ManagedDescriptor = typeof(IActor<ManagedActor>);
        readonly HashSet<ActorMailboxId> _running = [];
        readonly ActorMailboxId _supervisorId = new(ActorType.Query, "SupervisorQuery");
        readonly ActorMailboxId _managedId = new(ActorType.Query, "ManagedQuery");
        bool _ready;

        public Fixture(
            bool failManagedStartup = false,
            bool bootstrapRunning = true,
            bool pollerStops = true,
            bool failManagedStop = false,
            bool failActorRemoval = false,
            bool blockManagedStart = false,
            bool blockManagedStop = false,
            TimeSpan? actorShutdownTimeout = null)
        {
            var supervisorActor = Substitute.For<IActor>();
            supervisorActor.Id.Returns(_supervisorId);
            supervisorActor.IsRunning.Returns(_ => _running.Contains(_supervisorId));
            var managedActor = Substitute.For<IActor>();
            managedActor.Id.Returns(_managedId);
            managedActor.IsRunning.Returns(_ => _running.Contains(_managedId));

            var registry = Substitute.For<IActorRegistry>();
            registry.ActorTypes.Returns([SupervisorDescriptor, ManagedDescriptor]);
            var factory = Substitute.For<IActorFactory>();
            factory.GetActor(SupervisorDescriptor).Returns(supervisorActor);
            factory.GetActor(ManagedDescriptor).Returns(managedActor);

            var producer = Substitute.For<IActorProducer>();
            var consumer = Substitute.For<IActorConsumer>();
            var container = Substitute.For<IContainerInstance>();
            container.Resolve<IActorProducer>().Returns(producer);
            container.Resolve<IActorConsumer>().Returns(consumer);

            var actorSupervisor = ActorSupervisor = Substitute.For<IActorSupervisor>();
            actorSupervisor.Container.Returns(container);
            actorSupervisor.When(value => value.SetReadiness(Arg.Any<bool>()))
                .Do(call => _ready = call.Arg<bool>());
            actorSupervisor.When(value => value.AddActor(Arg.Any<IActor>()))
                .Do(call => Events.Add("register:" + call.Arg<IActor>().Id.Name));
            actorSupervisor.When(value => value.RemoveActor(Arg.Any<IActor>()))
                .Do(call =>
                {
                    Events.Add("remove:" + call.Arg<IActor>().Id.Name);
                    if (failActorRemoval)
                        throw new InvalidOperationException("injected removal failure");
                });
            actorSupervisor.StartAsync(Arg.Any<ActorMailboxId>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    var id = call.Arg<ActorMailboxId>();
                    Events.Add("start:" + id.Name);
                    if (failManagedStartup && id == _managedId)
                        return ValueTask.FromException(new InvalidOperationException("injected startup failure"));
                    if (blockManagedStart && id == _managedId)
                        return new ValueTask(CompleteBlockedStartAsync(id));
                    _running.Add(id);
                    return ValueTask.CompletedTask;
                });

            async Task CompleteBlockedStartAsync(ActorMailboxId id)
            {
                ManagedStartEntered.TrySetResult();
                await ReleaseManagedStart.Task.ConfigureAwait(false);
                _running.Add(id);
            }
            actorSupervisor.StartConsumersAsync(Arg.Any<CancellationToken>()).Returns(call =>
            {
                Events.Add("intake:start");
                return ValueTask.CompletedTask;
            });
            actorSupervisor.StopConsumersAsync(Arg.Any<CancellationToken>()).Returns(call =>
            {
                Events.Add("intake:stop");
                return ValueTask.CompletedTask;
            });
            actorSupervisor.StopAsync(Arg.Any<ActorMailboxId>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    var id = call.Arg<ActorMailboxId>();
                    Events.Add("stop:" + id.Name);
                    if (failManagedStop && id == _managedId)
                        return ValueTask.FromException(new InvalidOperationException("injected shutdown failure"));
                    if (blockManagedStop && id == _managedId && !ReleaseManagedStop.Task.IsCompleted)
                        return new ValueTask(ReleaseManagedStop.Task);
                    _running.Remove(id);
                    return ValueTask.CompletedTask;
                });
            actorSupervisor.ShutdownAsync(Arg.Any<CancellationToken>()).Returns(call =>
            {
                Events.Add("runtime:shutdown");
                _running.Clear();
                return ValueTask.CompletedTask;
            });

            var poller = Substitute.For<ISupervisorActorMetricsPollingService>();
            var pollerState = SupervisorPollingServiceState.Stopped;
            poller.State.Returns(_ => pollerState);
            poller.When(value => value.Start()).Do(_ =>
            {
                Events.Add("poller:start");
                pollerState = SupervisorPollingServiceState.Running;
            });
            poller.Stop(Arg.Any<TimeSpan>()).Returns(_ =>
            {
                Events.Add("poller:stop");
                if (pollerStops)
                    pollerState = SupervisorPollingServiceState.Stopped;
                return pollerStops;
            });

            var bootstrap = Substitute.For<ISupervisorBootstrap>();
            bootstrap.IsRunning.Returns(bootstrapRunning);
            bootstrap.ActorTypes.Returns([ActorType.Query]);

            Lifecycle = new(
                actorSupervisor, registry, factory, bootstrap, poller,
                new ActorRuntimeStartupOptions
                {
                    MaximumConcurrency = 1,
                    ActorShutdownTimeout = actorShutdownTimeout ?? TimeSpan.FromSeconds(5)
                },
                NullLogger<SupervisorManagedActorLifecycle>.Instance);
        }

        public List<string> Events { get; } = [];
        public TaskCompletionSource ManagedStartEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseManagedStart { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseManagedStop { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Ready => _ready;
        public IActorSupervisor ActorSupervisor { get; }
        public SupervisorManagedActorLifecycle Lifecycle { get; }
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
