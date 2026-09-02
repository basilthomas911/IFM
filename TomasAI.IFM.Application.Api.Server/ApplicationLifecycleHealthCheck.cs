using Microsoft.Extensions.Diagnostics.HealthChecks;
using TomasAI.IFM.Domain.Application.Shared;

namespace TomasAI.IFM.Application.Api.Server;

public sealed class ApplicationLifecycleHealthCheck(
    IApplicationStartupStatusStore statusStore) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var status = statusStore.Current;
        var data = new Dictionary<string, object>
        {
            ["state"] = status.State.ToString(),
            ["valueDate"] = status.ValueDate.ToString("yyyy-MM-dd"),
            ["processBootId"] = status.ProcessBootId,
            ["commandId"] = status.CommandId,
            ["correlationId"] = status.CorrelationId,
            ["activities"] = status.Activities.Length,
            ["summary"] = status.Summary
        };
        return Task.FromResult(status.State switch
        {
            ApplicationLifecycleState.Running or ApplicationLifecycleState.ScheduledStopped =>
                HealthCheckResult.Healthy(status.Summary, data),
            ApplicationLifecycleState.Degraded => HealthCheckResult.Degraded(status.Summary, data: data),
            ApplicationLifecycleState.Failed => HealthCheckResult.Unhealthy(status.Summary, data: data),
            _ => HealthCheckResult.Degraded(status.Summary, data: data)
        });
    }
}
