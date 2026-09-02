using Microsoft.Extensions.Diagnostics.HealthChecks;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Application.Api.Server;

public interface IApplicationBootstrapReadiness
{
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken);
}

/// <summary>Evaluates bootstrap-tagged checks without making a self-referential HTTP request.</summary>
public sealed class ApplicationBootstrapReadiness(
    HealthCheckService healthChecks,
    IActorSupervisor actorSupervisor) : IApplicationBootstrapReadiness
{
    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken)
    {
        if (!actorSupervisor.IsReady)
        {
            return false;
        }
        var report = await healthChecks.CheckHealthAsync(
            registration => registration.Tags.Contains("bootstrap"),
            cancellationToken).ConfigureAwait(false);
        return report.Status == HealthStatus.Healthy;
    }
}
