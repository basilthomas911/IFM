using Microsoft.Extensions.Diagnostics.HealthChecks;
using TomasAI.IFM.Application.Api.Server.Core.Development.Verification;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using Xunit;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

public sealed class PlannedClosurePipelineHealthTests
{
    [Theory]
    [InlineData("Healthy", null, "Healthy", HealthStatus.Healthy)]
    [InlineData("Inactive", null, "Healthy", HealthStatus.Healthy)]
    [InlineData("Inactive", null, "Inactive", HealthStatus.Healthy)]
    [InlineData("Inactive", null, "Degraded", HealthStatus.Degraded)]
    [InlineData("Inactive", null, "Unknown", HealthStatus.Degraded)]
    [InlineData("Inactive", null, "Unhealthy", HealthStatus.Degraded)]
    [InlineData("Unknown", null, "Healthy", HealthStatus.Degraded)]
    [InlineData("Inactive", "2026-11-20", "Healthy", HealthStatus.Degraded)]
    public void Planned_closure_requires_verified_components_and_no_active_session(string status,
        string? date, string componentStatus, HealthStatus expected)
    {
        var now = DateTime.UtcNow;
        var snapshot = new LivePipelineHealthSnapshot(now, date is null ? null : DateOnly.Parse(date), status,
            [new("Session authority", "session", "Healthy", "Authoritative session", now),
             new("MarketData dataset", "Futures", componentStatus, "Observed state", now)]);
        Assert.Equal(expected, LivePipelineHealthCheck.Evaluate(snapshot).Status);
    }

    [Fact]
    public void Missing_or_invalid_session_evidence_cannot_qualify_a_closed_pipeline()
    {
        Assert.Equal(HealthStatus.Degraded, LivePipelineHealthCheck.Evaluate(new(DateTime.UtcNow, null, "Inactive", [])).Status);
        Assert.Equal(HealthStatus.Degraded, LivePipelineHealthCheck.Evaluate(new(DateTime.UtcNow, null, "Inactive",
            [new("Session authority", "session", "Unhealthy", "Invalid session", DateTime.UtcNow)])).Status);
    }
}
