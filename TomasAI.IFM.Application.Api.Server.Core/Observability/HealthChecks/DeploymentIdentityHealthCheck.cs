using TomasAI.IFM.Application.Api.Server.Core.Deployment.Identity;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TomasAI.IFM.Application.Api.Server.Core.Observability.HealthChecks;

public sealed class DeploymentIdentityHealthCheck(DeploymentIdentityMonitor monitor) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var validation = monitor.Current;
        var data = new Dictionary<string, object>
        {
            ["enforced"] = validation.Enforced,
            ["buildId"] = validation.BuildId,
            ["manifestPath"] = validation.ManifestPath,
            ["capturedAtUtc"] = validation.CapturedAtUtc.ToString("O"),
            ["artifacts"] = validation.ProcessStartHashes,
            ["errors"] = validation.Errors
        };

        return Task.FromResult(validation.Valid
            ? HealthCheckResult.Healthy(validation.Enforced
                ? "The running process matches the current deployment manifest."
                : "Deployment identity enforcement is not applicable to this in-process test host.", data)
            : HealthCheckResult.Unhealthy(
                "The running process does not match the current deployment manifest.", data: data));
    }
}
