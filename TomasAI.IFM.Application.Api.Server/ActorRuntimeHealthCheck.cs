using Microsoft.Extensions.Diagnostics.HealthChecks;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Application.Api.Server;

/// <summary>
/// Reports whether actor initialization has completed and external message intake is safe.
/// </summary>
public sealed class ActorRuntimeHealthCheck(IActorSupervisor supervisor) : IHealthCheck
{
    readonly IActorSupervisor _supervisor = supervisor ?? throw new ArgumentNullException(nameof(supervisor));

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_supervisor.IsReady
            ? HealthCheckResult.Healthy("Actor runtime is ready and consumer intake is open.")
            : HealthCheckResult.Unhealthy("Actor runtime startup is incomplete or intake is closed."));
}
