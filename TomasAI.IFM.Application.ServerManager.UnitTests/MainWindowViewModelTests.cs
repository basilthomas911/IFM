using FluentAssertions;
using TomasAI.IFM.Application.ServerManager.Contracts;

namespace TomasAI.IFM.Application.ServerManager.UnitTests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void AddLog_keeps_only_the_configured_newest_entries()
    {
        var viewModel = new MainWindowViewModel(
            new ServerManagerOptions { MaximumLogEntries = 3 },
            new ImmediateDispatcher(),
            new StubSchedulerClient());

        for (var index = 0; index < 10; index++)
        {
            viewModel.AddLog(new ManagedProcessLogEntry(
                DateTimeOffset.UnixEpoch.AddSeconds(index),
                "api",
                "API Server",
                ManagedProcessLogStream.StandardOutput,
                $"line-{index}"));
        }

        viewModel.ConsoleStatus.Select(entry => entry.Message)
            .Should().Equal("line-9", "line-8", "line-7");
    }

    [Fact]
    public void AddLog_preserves_process_stream_and_timestamp()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var viewModel = new MainWindowViewModel(
            new ServerManagerOptions { MaximumLogEntries = 3 },
            new ImmediateDispatcher(),
            new StubSchedulerClient());

        viewModel.AddLog(new ManagedProcessLogEntry(
            timestamp,
            "ui",
            "UI.Net",
            ManagedProcessLogStream.StandardError,
            "failure"));

        viewModel.ConsoleStatus.Should().ContainSingle().Which.Should().BeEquivalentTo(new StatusLog
        {
            Timestamp = timestamp,
            ProcessName = "UI.Net",
            Stream = ManagedProcessLogStream.StandardError,
            Message = "failure"
        });
    }

    [Fact]
    public void AddLog_bounds_entries_waiting_for_the_ui_dispatcher_and_reports_drops()
    {
        var dispatcher = new QueuedDispatcher();
        var viewModel = new MainWindowViewModel(
            new ServerManagerOptions { MaximumLogEntries = 3 },
            dispatcher,
            new StubSchedulerClient());

        for (var index = 0; index < 10; index++)
        {
            viewModel.AddLog(new ManagedProcessLogEntry(
                DateTimeOffset.UnixEpoch.AddSeconds(index),
                "api",
                "API Server",
                ManagedProcessLogStream.StandardOutput,
                $"line-{index}"));
        }

        dispatcher.RunPending();

        viewModel.ConsoleStatus.Should().HaveCount(3);
        viewModel.ConsoleStatus.Should().Contain(entry => entry.Message.Contains("Dropped 7 pending log entries"));
        viewModel.ConsoleStatus.Should().Contain(entry => entry.Message == "line-9");
    }

    [Fact]
    public async Task RefreshScheduler_populates_the_read_only_dashboard()
    {
        var dashboard = new SchedulerDashboardDto(
            new SchedulerHealthDto(
                SchedulerServiceState.Ready,
                "1.0.0",
                true,
                true,
                true,
                "ready",
                DateTimeOffset.UtcNow),
            [new TaskCatalogItemDto("task", "Task", "Description", "task.exe", "Development", SchedulerRiskClassification.Maintenance, "1", true, 30)],
            [],
            [],
            DateTimeOffset.UtcNow);
        var viewModel = new MainWindowViewModel(
            new ServerManagerOptions { MaximumLogEntries = 3 },
            new ImmediateDispatcher(),
            new StubSchedulerClient(dashboard));

        await viewModel.RefreshSchedulerAsync();

        viewModel.SchedulerState.Should().Be("Ready");
        viewModel.SchedulerDatabaseAvailable.Should().BeTrue();
        viewModel.QuartzAvailable.Should().BeTrue();
        viewModel.TaskCatalog.Should().ContainSingle().Which.TaskKey.Should().Be("task");
    }

    private sealed class ImmediateDispatcher : IUiDispatcher
    {
        public void Post(Action action) => action();
    }

    private sealed class QueuedDispatcher : IUiDispatcher
    {
        private Action? _pending;

        public void Post(Action action) => _pending += action;

        public void RunPending()
        {
            var pending = _pending;
            _pending = null;
            pending?.Invoke();
        }
    }

    private sealed class StubSchedulerClient(SchedulerDashboardDto? dashboard = null) : ISchedulerDashboardClient
    {
        public Task<SchedulerDashboardDto> GetDashboardAsync(CancellationToken cancellationToken)
            => Task.FromResult(dashboard ?? new SchedulerDashboardDto(
                new SchedulerHealthDto(
                    SchedulerServiceState.Unhealthy,
                    "unknown",
                    false,
                    false,
                    false,
                    "offline",
                    DateTimeOffset.UtcNow),
                [],
                [],
                [],
                DateTimeOffset.UtcNow));
    }
}
