using TomasAI.IFM.Application.Api.Server.Core.Observability.HealthChecks;
using TomasAI.IFM.Application.Api.Server.Core.Startup.Application;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TomasAI.IFM.Domain.Application.Event;
using TomasAI.IFM.Domain.Application.Shared;

namespace TomasAI.IFM.Domain.Application.Actor.UnitTests;

public sealed class ApplicationLifecycleHealthCheckTests
{
    [Fact]
    public async Task Launch_allows_pending_optional_work_only_after_all_required_checks_pass()
    {
        var lifecycle = new ApplicationStartupStatusStore();
        var handoff = new ApplicationStartupHandoffStatusStore();
        handoff.Set(new() { State = ApplicationStartupHandoffState.LifecycleObserved });
        var launch = new ApplicationLifecycleHealthCheck(lifecycle, handoff, allowPendingOptionalActivities: true);
        var full = new ApplicationLifecycleHealthCheck(lifecycle, handoff);
        var context = new HealthCheckContext();
        var required = ApplicationStartupPlan.Activities.Where(definition => definition.Required)
            .Select(definition => new ApplicationStartupActivityResult
            {
                Activity = definition.Activity, Required = true, Outcome = ApplicationStartupActivityOutcome.AlreadySatisfied
            }).ToArray();
        lifecycle.Set(new() { State = ApplicationLifecycleState.Starting, Activities = required[..^1] });
        Assert.Equal(HealthStatus.Unhealthy, (await launch.CheckHealthAsync(context)).Status);
        lifecycle.Set(lifecycle.Current with { Activities = required });
        Assert.Equal(HealthStatus.Healthy, (await launch.CheckHealthAsync(context)).Status);
        Assert.Equal(HealthStatus.Unhealthy, (await full.CheckHealthAsync(context)).Status);
        required[^1] = required[^1] with { Outcome = ApplicationStartupActivityOutcome.Failed };
        lifecycle.Set(lifecycle.Current with { Activities = [.. required] });
        Assert.Equal(HealthStatus.Unhealthy, (await launch.CheckHealthAsync(context)).Status);
    }

    [Fact]
    public async Task Cached_health_invalidates_on_either_lifecycle_or_handoff_transition()
    {
        var lifecycle = new ApplicationStartupStatusStore();
        lifecycle.Set(new() { State = ApplicationLifecycleState.Running, Summary = "Running" });
        var handoff = new ApplicationStartupHandoffStatusStore();
        handoff.Set(new() { State = ApplicationStartupHandoffState.LifecycleObserved });
        var check = new ApplicationLifecycleHealthCheck(lifecycle, handoff);
        var context = new HealthCheckContext();
        var original = check.CheckHealthAsync(context);
        Assert.Same(original, check.CheckHealthAsync(context));
        Assert.Equal(HealthStatus.Healthy, (await original).Status);
        handoff.Set(handoff.Current with { State = ApplicationStartupHandoffState.TimedOut, LastError = "handoff lost" });
        var failed = await check.CheckHealthAsync(context);
        Assert.Equal(HealthStatus.Unhealthy, failed.Status);
        Assert.Equal("handoff lost", failed.Data["handoffLastError"]);
        handoff.Set(handoff.Current with { State = ApplicationStartupHandoffState.LifecycleObserved });
        lifecycle.Set(lifecycle.Current with { State = ApplicationLifecycleState.Failed, Summary = "Stopped unexpectedly" });
        var stopped = await check.CheckHealthAsync(context);
        Assert.Equal(HealthStatus.Unhealthy, stopped.Status);
        Assert.Equal("Stopped unexpectedly", stopped.Description);
        var data = Assert.IsAssignableFrom<IDictionary<string, object>>(stopped.Data);
        Assert.Throws<NotSupportedException>(() => data["summary"] = "corrupted");
    }

    [Fact]
    public async Task Accepted_but_unobserved_handoff_replaces_not_requested_description()
    {
        var commandId = Guid.NewGuid();
        var handoff = new ApplicationStartupHandoffStatusStore();
        handoff.Set(new()
        {
            State = ApplicationStartupHandoffState.TimedOut,
            ValueDate = new(2026, 9, 4),
            CommandId = commandId,
            AttemptCount = 3,
            Summary = "Application startup was accepted but its lifecycle event has not been observed."
        });
        var check = new ApplicationLifecycleHealthCheck(new ApplicationStartupStatusStore(), handoff);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal(handoff.Current.Summary, result.Description);
        Assert.Equal(ApplicationStartupHandoffState.TimedOut.ToString(), result.Data["handoffState"]);
        Assert.Equal(commandId, result.Data["handoffCommandId"]);
    }

    [Fact]
    public async Task Actor_owned_terminal_status_remains_authoritative()
    {
        var lifecycle = new ApplicationStartupStatusStore();
        lifecycle.Set(new()
        {
            State = ApplicationLifecycleState.Running,
            ValueDate = new(2026, 9, 4),
            CommandId = Guid.NewGuid(),
            Summary = "Application startup Running."
        });
        var handoff = new ApplicationStartupHandoffStatusStore();
        handoff.Set(new()
        {
            State = ApplicationStartupHandoffState.LifecycleObserved,
            Summary = "Lifecycle observed."
        });
        var check = new ApplicationLifecycleHealthCheck(lifecycle, handoff);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal(lifecycle.Current.Summary, result.Description);
    }
}
