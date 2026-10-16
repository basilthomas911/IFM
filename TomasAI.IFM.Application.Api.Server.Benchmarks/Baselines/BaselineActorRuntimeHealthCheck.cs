// Frozen from commit 51075e3eb before this optimization pass. Benchmark reference only.
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime;
using TomasAI.IFM.Shared.EventModelActor;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.Baselines;

/// <summary>
/// Reports whether actor initialization has completed and external message intake is safe.
/// </summary>
public sealed class BaselineActorRuntimeHealthCheck(
    IActorSupervisor supervisor,
    IActorRegistry registry) : IHealthCheck
{
    readonly IActorSupervisor _supervisor = supervisor ?? throw new ArgumentNullException(nameof(supervisor));
    readonly IActorRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>
        {
            ["registeredActorTypes"] = _registry.ActorTypes.Length
        };
        return Task.FromResult(_supervisor.IsReady
            ? HealthCheckResult.Healthy(
                "Actor runtime is ready and consumer intake is open.",
                data)
            : HealthCheckResult.Unhealthy(
                "Actor runtime startup is incomplete or intake is closed.",
                data: data));
    }
}
