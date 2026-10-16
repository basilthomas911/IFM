using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Runtime.CompilerServices;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Application.Api.Server.Core.Observability.HealthChecks;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Application.Actor.UnitTests;

public sealed class ActorReadinessHealthCacheTests
{
    [Fact]
    public async Task Readiness_and_registry_changes_are_visible_without_cache_ttl()
    {
        // Exercise the real readiness flag without starting actor infrastructure.
        var supervisor = (ActorSupervisor)RuntimeHelpers.GetUninitializedObject(typeof(ActorSupervisor));
        var registry = new Registry { ActorTypes = [typeof(ActorReadinessHealthCacheTests)] };
        var check = new ActorRuntimeHealthCheck(supervisor, registry);
        var context = new HealthCheckContext();
        Assert.Equal(HealthStatus.Unhealthy, (await check.CheckHealthAsync(context)).Status);
        supervisor.SetReadiness(true);
        var ready = check.CheckHealthAsync(context);
        Assert.Same(ready, check.CheckHealthAsync(context));
        Assert.Equal(HealthStatus.Healthy, (await ready).Status);
        Assert.Equal(1, (await ready).Data["registeredActorTypes"]);
        registry.ActorTypes = [typeof(int), typeof(string)];
        Assert.Equal(2, (await check.CheckHealthAsync(context)).Data["registeredActorTypes"]);
        supervisor.SetReadiness(false);
        Assert.Equal(HealthStatus.Unhealthy, (await check.CheckHealthAsync(context)).Status);
        var data = Assert.IsAssignableFrom<IDictionary<string, object>>((await ready).Data);
        Assert.Throws<NotSupportedException>(() => data["registeredActorTypes"] = 999);
    }

    sealed class Registry : IActorRegistry
    {
        public Type[] ActorTypes { get; set; } = [];
    }
}
