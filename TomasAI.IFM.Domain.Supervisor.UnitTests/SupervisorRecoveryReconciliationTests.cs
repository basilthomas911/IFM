using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Domain.Supervisor.Shared.Service.Lifecycle;
using TomasAI.IFM.Domain.Supervisor.Health.Query.Actor;
using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Supervisor.UnitTests;

public sealed class SupervisorRecoveryReconciliationTests
{
    const string ManagedActorId = "Query.ManagedQuery";

    [Fact]
    public async Task Stopped_owned_actor_is_restarted_and_healthy_actor_is_kept()
    {
        await using var fixture = new Fixture();
        Assert.True((await fixture.Lifecycle.StartupActorsAsync(CancellationToken.None)).Succeeded);
        var first = await fixture.Lifecycle.ReconcileAsync(TimeSpan.FromSeconds(2), CancellationToken.None);
        Assert.True(first.Qualified, first.Detail);
        Assert.All(first.Components, component => Assert.Equal("Keep", component.Action));

        fixture.Managed.SetRunning(false);
        var second = await fixture.Lifecycle.ReconcileAsync(TimeSpan.FromSeconds(2), CancellationToken.None);

        Assert.True(second.Qualified, second.Detail);
        Assert.Contains(second.Components, component => component.Component == ManagedActorId
            && component.Action == "Recycle");
        Assert.Equal(2, fixture.Managed.Starts);
        Assert.Equal(0, fixture.Managed.Stops);
    }

    [Fact]
    public async Task Stalled_owned_projector_is_recycled_through_its_actor()
    {
        await using var fixture = new Fixture();
        Assert.True((await fixture.Lifecycle.StartupActorsAsync(CancellationToken.None)).Succeeded);
        fixture.Projector.IsReady = false;
        fixture.Projector.ResetOnOwnerRestart = true;

        var result = await fixture.Lifecycle.ReconcileAsync(TimeSpan.FromSeconds(2), CancellationToken.None);

        Assert.True(result.Qualified, result.Detail);
        Assert.Equal(2, fixture.Managed.Starts);
        Assert.Equal(1, fixture.Managed.Stops);
        Assert.Contains(result.Components, component => component.Component == "ManagedQuery:Projection"
            && component.Action == "Recycle" && component.Health == SupervisorActorHealth.Healthy);
    }

    [Fact]
    public async Task Timed_out_projector_stop_never_starts_an_overlapping_actor()
    {
        await using var fixture = new Fixture();
        Assert.True((await fixture.Lifecycle.StartupActorsAsync(CancellationToken.None)).Succeeded);
        fixture.Projector.IsReady = false;
        fixture.Managed.BlockStop = true;

        var result = await fixture.Lifecycle.ReconcileAsync(TimeSpan.FromMilliseconds(50), CancellationToken.None);

        Assert.False(result.Qualified);
        Assert.Equal(1, fixture.Managed.Starts);
        Assert.Contains(result.Components, component => component.Action == "Deadline");
        fixture.Managed.ReleaseStop();
    }

    [Fact]
    public async Task Projector_snapshot_exception_is_a_typed_critical_result()
    {
        await using var fixture = new Fixture();
        Assert.True((await fixture.Lifecycle.StartupActorsAsync(CancellationToken.None)).Succeeded);
        fixture.Projector.ThrowOnSnapshot = true;

        var result = await fixture.Lifecycle.ReconcileAsync(TimeSpan.FromSeconds(2), CancellationToken.None);

        Assert.False(result.Qualified);
        Assert.Contains(result.Components, component => component.Action == "Exception"
            && component.Health == SupervisorActorHealth.Critical);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ActorSupervisor supervisor;

        public Fixture()
        {
            var container = Substitute.For<IContainerInstance>();
            container.Resolve<IActorProducer>().Returns(Substitute.For<IActorProducer>());
            container.Resolve<IActorConsumer>().Returns(Substitute.For<IActorConsumer>());
            supervisor = new ActorSupervisor(container, NullLogger<ActorSupervisor>.Instance);
            Managed = new ManagedActor(supervisor);
            Projector = new ProjectorSource(Managed);
            supervisor.RuntimeContext.RegisterProjector(Projector);
            var registry = Substitute.For<IActorRegistry>();
            registry.ActorTypes.Returns([typeof(IActor<SupervisorQueryActor>), typeof(IActor<ManagedActor>)]);
            var factory = Substitute.For<IActorFactory>();
            factory.GetActor(typeof(IActor<ManagedActor>)).Returns(Managed);
            var bootstrap = Substitute.For<ISupervisorBootstrap>();
            bootstrap.IsRunning.Returns(true);
            bootstrap.ActorTypes.Returns([ActorType.Query]);
            var poller = Substitute.For<ISupervisorActorMetricsPollingService>();
            poller.State.Returns(SupervisorPollingServiceState.Running);
            poller.Stop(Arg.Any<TimeSpan>()).Returns(true);
            Lifecycle = new SupervisorManagedActorLifecycle(supervisor, registry, factory,
                bootstrap, poller, new ActorRuntimeStartupOptions { MaximumConcurrency = 1 },
                NullLogger<SupervisorManagedActorLifecycle>.Instance);
        }

        public ManagedActor Managed { get; }
        public ProjectorSource Projector { get; }
        public SupervisorManagedActorLifecycle Lifecycle { get; }

        public async ValueTask DisposeAsync()
        {
            Managed.ReleaseStop();
            await Lifecycle.ShutdownActorsAsync(CancellationToken.None);
            await supervisor.DisposeAsync();
        }
    }

    sealed class ManagedActor(IActorSupervisor supervisor) : IActor<ManagedActor>
    {
        readonly TaskCompletionSource releaseStop = new(TaskCreationOptions.RunContinuationsAsynchronously);
        bool running;
        public ActorMailboxId Id { get; } = new(ActorType.Query, "ManagedQuery");
        public IActorMailbox Mailbox { get; } = new ActorMailbox(supervisor, new(ActorType.Query, "ManagedQuery"));
        public bool IsRunning => running;
        public bool BlockStop { get; set; }
        public int Starts { get; private set; }
        public int Stops { get; private set; }
        public void SetRunning(bool value) => running = value;
        public void ReleaseStop() => releaseStop.TrySetResult();
        public ValueTask HandleMessageAsync(IActorMessage message) => ValueTask.CompletedTask;
        public ValueTask StartAsync(IActorSupervisor actorSupervisor)
        {
            Starts++;
            running = true;
            return ValueTask.CompletedTask;
        }
        public async ValueTask StopAsync()
        {
            Stops++;
            if (BlockStop) await releaseStop.Task.ConfigureAwait(false);
            running = false;
        }
    }

    sealed class ProjectorSource(ManagedActor owner) : ISupervisorProjectorMetricsSource
    {
        public string SupervisorProjectorKey => "ManagedQuery/Projection";
        public bool IsReady { get; set; } = true;
        public bool ResetOnOwnerRestart { get; set; }
        public bool ThrowOnSnapshot { get; set; }
        public SupervisorProjectorSnapshot CaptureSupervisorSnapshot()
        {
            if (ThrowOnSnapshot) throw new InvalidOperationException("Injected projector snapshot failure.");
            return new(owner.Id.Name, "Projection", "process", "replay",
                (IsReady || (ResetOnOwnerRestart && owner.Starts > 1)) && owner.IsRunning,
                0, 0, DateTime.UtcNow, string.Empty);
        }
    }
}
