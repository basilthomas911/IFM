using TomasAI.IFM.Domain.Supervisor.Context;
using TomasAI.IFM.Domain.Supervisor.Health;
using TomasAI.IFM.Domain.Supervisor.Lifecycle;
using TomasAI.IFM.Domain.Supervisor.Metrics;
using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.ReadModels;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;

namespace TomasAI.IFM.Domain.Supervisor.UnitTests;

public sealed class SupervisorCapabilityBoundaryTests
{
    [Fact]
    public void Privileged_context_exposes_only_named_capabilities()
    {
        var properties = typeof(ISupervisorActorContext).GetProperties();

        Assert.Equal(6, properties.Length);
        Assert.DoesNotContain(properties, property => property.Name.Contains("Container", StringComparison.Ordinal));
        Assert.DoesNotContain(properties, property => property.Name.Contains("Runtime", StringComparison.Ordinal));
        Assert.DoesNotContain(properties, property => property.Name.Contains("Mailbox", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Observe_only_lifecycle_never_mutates_or_throws_for_cancellation()
    {
        var lifecycle = new ObserveOnlySupervisorManagedActorLifecycle();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var startup = await lifecycle.StartupActorsAsync(cancellation.Token);
        var shutdown = await lifecycle.ShutdownActorsAsync(cancellation.Token);

        Assert.Equal(SupervisorOperationOutcome.Cancelled, startup.Outcome);
        Assert.Equal(SupervisorOperationOutcome.Cancelled, shutdown.Outcome);
    }

    [Fact]
    public void Context_can_be_constructed_without_runtime_root_access()
    {
        var context = new SupervisorActorContext(
            new ObserveOnlySupervisorManagedActorLifecycle(),
            new SupervisorActorMetricsState(),
            new ObserveOnlySupervisorHealthManager(),
            new EmptySupervisorIncidentStore(),
            new EmptySupervisorOperationStore(),
            new EmptySupervisorHistoryStore());

        Assert.Equal(SupervisorAuthorityState.Available, context.Health.AuthorityState);
        Assert.Same(SupervisorActorMetricsSnapshot.Empty, context.ActorMetrics.Current);
    }
}
