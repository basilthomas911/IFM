using FluentAssertions;
using TomasAI.IFM.Application.ServerManager.Contracts;

namespace TomasAI.IFM.Application.ServerManager.UnitTests;

public sealed class SchedulerPipeClientTests
{
    [Fact]
    public async Task GetDashboard_returns_offline_when_optional_host_does_not_accept_connection()
    {
        var options = new SchedulerClientOptions
        {
            Enabled = true,
            PipeName = $"IFM.ServerManager.Missing.{Guid.NewGuid():N}",
            ConnectTimeoutMilliseconds = 25
        };

        var dashboard = await new SchedulerPipeClient(options).GetDashboardAsync(CancellationToken.None);

        dashboard.Health.State.Should().Be(SchedulerServiceState.Unhealthy);
        dashboard.Health.Message.Should().Contain("offline").And.Contain("25 ms");
        dashboard.TaskCatalog.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDashboard_preserves_caller_cancellation()
    {
        var options = new SchedulerClientOptions
        {
            Enabled = true,
            PipeName = $"IFM.ServerManager.Missing.{Guid.NewGuid():N}",
            ConnectTimeoutMilliseconds = 10_000
        };
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var act = () => new SchedulerPipeClient(options).GetDashboardAsync(cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
