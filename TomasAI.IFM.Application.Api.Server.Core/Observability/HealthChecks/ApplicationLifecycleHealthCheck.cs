using TomasAI.IFM.Application.Api.Server.Core.Startup.Application;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Collections.ObjectModel;
using TomasAI.IFM.Domain.Application.Shared;

namespace TomasAI.IFM.Application.Api.Server.Core.Observability.HealthChecks;

public sealed class ApplicationLifecycleHealthCheck(
    IApplicationStartupStatusStore statusStore,
    IApplicationStartupHandoffStatusStore handoffStatusStore) : IHealthCheck
{
    sealed record PublishedResult(ApplicationStartupStatus Status, ApplicationStartupHandoffStatus Handoff, Task<HealthCheckResult> Result);
    PublishedResult? published;
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var status = statusStore.Current;
        var handoff = handoffStatusStore.Current;
        var cached = Volatile.Read(ref published);
        if (cached is not null && ReferenceEquals(cached.Status, status) && ReferenceEquals(cached.Handoff, handoff)) return cached.Result;
        var description = status.State == ApplicationLifecycleState.Bootstrapped
            && handoff.State != ApplicationStartupHandoffState.NotAttempted
                ? handoff.Summary
                : status.Summary;
        var data = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>
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
        });
        var result = Task.FromResult(handoff.State != ApplicationStartupHandoffState.LifecycleObserved
            ? HealthCheckResult.Unhealthy(description, data: data)
            : status.State switch
        {
            ApplicationLifecycleState.Running or ApplicationLifecycleState.ScheduledStopped =>
                HealthCheckResult.Healthy(description, data),
            ApplicationLifecycleState.Degraded => HealthCheckResult.Healthy(description, data),
            ApplicationLifecycleState.Failed => HealthCheckResult.Unhealthy(description, data: data),
            _ => HealthCheckResult.Unhealthy(description, data: data)
        });
        Volatile.Write(ref published, new(status, handoff, result));
        return result;
    }
}
