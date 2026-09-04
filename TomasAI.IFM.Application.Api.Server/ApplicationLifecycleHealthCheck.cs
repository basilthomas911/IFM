using Microsoft.Extensions.Diagnostics.HealthChecks;
using TomasAI.IFM.Domain.Application.Shared;

namespace TomasAI.IFM.Application.Api.Server;

public sealed class ApplicationLifecycleHealthCheck(
    IApplicationStartupStatusStore statusStore,
    IApplicationStartupHandoffStatusStore handoffStatusStore) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var status = statusStore.Current;
        var handoff = handoffStatusStore.Current;
        var description = status.State == ApplicationLifecycleState.Bootstrapped
            && handoff.State != ApplicationStartupHandoffState.NotAttempted
                ? handoff.Summary
                : status.Summary;
        var data = new Dictionary<string, object>
        {
            ["state"] = status.State.ToString(),
            ["valueDate"] = status.ValueDate.ToString("yyyy-MM-dd"),
            ["processBootId"] = status.ProcessBootId,
            ["commandId"] = status.CommandId,
            ["correlationId"] = status.CorrelationId,
            ["activities"] = status.Activities.Length,
            ["summary"] = description,
            ["handoffState"] = handoff.State.ToString(),
            ["handoffCommandId"] = handoff.CommandId,
            ["handoffAttemptCount"] = handoff.AttemptCount,
            ["handoffAcceptedAtUtc"] = handoff.AcceptedAtUtc?.ToString("O") ?? string.Empty,
            ["handoffObservationDeadlineUtc"] = handoff.ObservationDeadlineUtc?.ToString("O") ?? string.Empty,
            ["handoffLastError"] = handoff.LastError,
            ["handoffObservedAtUtc"] = handoff.ObservedAtUtc?.ToString("O") ?? string.Empty
        };
        return Task.FromResult(status.State switch
        {
            ApplicationLifecycleState.Running or ApplicationLifecycleState.ScheduledStopped =>
                HealthCheckResult.Healthy(description, data),
            ApplicationLifecycleState.Degraded => HealthCheckResult.Degraded(description, data: data),
            ApplicationLifecycleState.Failed => HealthCheckResult.Unhealthy(description, data: data),
            _ => HealthCheckResult.Degraded(description, data: data)
        });
    }
}
