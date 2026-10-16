using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Collections.ObjectModel;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Application.Api.Server.Core.Observability.HealthChecks;

/// <summary>
/// Reports whether actor initialization has completed and external message intake is safe.
/// </summary>
public sealed class ActorRuntimeHealthCheck(
    IActorSupervisor supervisor,
    IActorRegistry registry) : IHealthCheck
{
    readonly IActorSupervisor _supervisor = supervisor ?? throw new ArgumentNullException(nameof(supervisor));
    sealed record PublishedResult(bool Ready, int Count, Task<HealthCheckResult> Result);
    PublishedResult? published;
    readonly IActorRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var ready = _supervisor.IsReady;
        var count = _registry.ActorTypes.Length;
        var cached = Volatile.Read(ref published);
        if (cached is not null && cached.Ready == ready && cached.Count == count) return cached.Result;
        var data = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>
        {
            ["registeredActorTypes"] = count
        });
        var result = Task.FromResult(ready
            ? HealthCheckResult.Healthy(
                "Actor runtime is ready and consumer intake is open.",
                data)
            : HealthCheckResult.Unhealthy(
                "Actor runtime startup is incomplete or intake is closed.",
                data: data));
        Volatile.Write(ref published, new(ready, count, result));
        return result;
    }
}
