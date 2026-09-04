using Microsoft.Extensions.Diagnostics.HealthChecks;
using TomasAI.IFM.Application.Api.Server;
using TomasAI.IFM.Domain.Application.Actor.Event;
using TomasAI.IFM.Domain.Application.Shared;

namespace TomasAI.IFM.Domain.Application.Actor.UnitTests;

public sealed class ApplicationLifecycleHealthCheckTests
{
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

        Assert.Equal(HealthStatus.Degraded, result.Status);
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
