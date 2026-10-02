using TomasAI.IFM.Domain.Supervisor.Operations.Command.Actor;
using TomasAI.IFM.Domain.Supervisor.Shared;
using TomasAI.IFM.Domain.Supervisor.Shared.Service.Lifecycle;
using TomasAI.IFM.Domain.Supervisor.Shared.Service;
using TomasAI.IFM.Domain.Supervisor.Health.Query.Actor;
using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;

namespace TomasAI.IFM.Domain.Supervisor.UnitTests;

public sealed class SupervisorCapabilityBoundaryTests
{
    [Fact]
    public void Command_context_exposes_only_lifecycle_incident_authorization_and_logging_capabilities()
    {
        var names = typeof(ISupervisorCommandActorContext).GetProperties()
            .Select(property => property.Name).OrderBy(name => name).ToArray();

        Assert.Equal(["Authorizer", "EventProjector", "Incidents", "Logger",
            "ManagedActors", "StateRepository"], names);
    }

    [Fact]
    public void Command_incident_capability_is_read_only_and_poller_is_shared()
    {
        Assert.Equal(typeof(ISupervisorIncidentReadStore),
            typeof(ISupervisorCommandActorContext).GetProperty("Incidents")!.PropertyType);
        Assert.DoesNotContain(typeof(ISupervisorIncidentReadStore).GetMethods(),
            method => method.Name == "Acknowledge");
        Assert.Equal(typeof(ISupervisorIncidentReadStore).Assembly,
            typeof(SupervisorActorMetricsPollingService).Assembly);
        Assert.Equal(SupervisorSharedAssembly.ActorAssemblyName,
            typeof(SupervisorCommandActor).Assembly.GetName().Name);
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
    public void Query_context_exposes_only_metrics_and_logging_capabilities()
    {
        var names = typeof(ISupervisorQueryActorContext).GetProperties()
            .Select(property => property.Name).OrderBy(name => name).ToArray();

        Assert.Equal(["ActorMetrics", "Logger"], names);
    }
}
