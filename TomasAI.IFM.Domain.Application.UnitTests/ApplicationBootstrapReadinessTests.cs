using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TomasAI.IFM.Application.Api.Server;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Application.UnitTests;

public sealed class ApplicationBootstrapReadinessTests
{
    [Fact]
    public async Task Actor_startup_short_circuits_bootstrap_health_checks()
    {
        var checkRuns = 0;
        using var services = CreateServices(() => checkRuns++);
        var readiness = new ApplicationBootstrapReadiness(
            services.GetRequiredService<HealthCheckService>(),
            CreateSupervisor(isReady: false));

        var healthy = await readiness.IsHealthyAsync(CancellationToken.None);

        Assert.False(healthy);
        Assert.Equal(0, checkRuns);
    }

    [Fact]
    public async Task Bootstrap_health_checks_run_after_actor_startup_completes()
    {
        var checkRuns = 0;
        using var services = CreateServices(() => checkRuns++);
        var readiness = new ApplicationBootstrapReadiness(
            services.GetRequiredService<HealthCheckService>(),
            CreateSupervisor(isReady: true));

        var healthy = await readiness.IsHealthyAsync(CancellationToken.None);

        Assert.True(healthy);
        Assert.Equal(1, checkRuns);
    }

    private static ServiceProvider CreateServices(Action onCheck)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHealthChecks().AddCheck(
            "bootstrap_probe",
            () =>
            {
                onCheck();
                return HealthCheckResult.Healthy();
            },
            tags: ["bootstrap"]);
        return services.BuildServiceProvider();
    }

    private static IActorSupervisor CreateSupervisor(bool isReady)
    {
        var supervisor = DispatchProxy.Create<IActorSupervisor, ActorSupervisorProxy>();
        ((ActorSupervisorProxy)(object)supervisor).Ready = isReady;
        return supervisor;
    }

    public class ActorSupervisorProxy : DispatchProxy
    {
        public bool Ready { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "get_IsReady")
            {
                return Ready;
            }
            throw new NotSupportedException(targetMethod?.Name);
        }
    }
}
