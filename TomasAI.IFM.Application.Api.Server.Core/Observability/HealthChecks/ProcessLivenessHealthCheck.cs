using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TomasAI.IFM.Application.Api.Server.Core.Observability.HealthChecks;

/// <summary>Reports only that the process can execute the HTTP request pipeline.</summary>
public sealed class ProcessLivenessHealthCheck : IHealthCheck
{
    static readonly Task<HealthCheckResult> Healthy = Task.FromResult(
        HealthCheckResult.Healthy("The API process is responsive."));

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default) => Healthy;
}
