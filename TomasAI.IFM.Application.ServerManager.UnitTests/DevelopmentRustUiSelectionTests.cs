using System.Diagnostics;
using FluentAssertions;
using Xunit;

namespace TomasAI.IFM.Application.ServerManager.UnitTests;

public sealed class DevelopmentRustUiSelectionTests
{
    static ServerManagerOptions Options() => new()
    {
        Processes = [
            new() { Key = "api", DisplayName = "API", WorkingDirectory = @"C:\apps\api", ExecutablePath = "api.exe", StartOrder = 10, ReadinessUri = "http://localhost:22543/health/launch-ready" },
            new() { Key = "ui", DisplayName = "UI.Net", WorkingDirectory = @"C:\apps\ui", ExecutablePath = "ui.exe", StartOrder = 20, ShutdownMode = ProcessShutdownMode.CloseMainWindow },
            new() { Key = "scheduler", DisplayName = "Scheduler", WorkingDirectory = @"C:\apps\scheduler", ExecutablePath = "scheduler.exe", StartOrder = 30 }
        ]
    };

    [Fact]
    public void Rust_selection_preserves_api_readiness_scheduler_and_ui_ownership_key()
    {
        var options = Options();
        var api = options.Processes[0];
        var scheduler = options.Processes[2];
        options.SelectDevelopmentUi("Development", "Rust", @"C:\repos\IFM");
        options.Validate();
        options.Processes[0].Should().BeSameAs(api);
        api.ReadinessUri.Should().Be("http://localhost:22543/health/launch-ready");
        options.Processes[2].Should().BeSameAs(scheduler);
        var ui = options.Processes.Single(p => p.Key == "ui");
        ui.ResolveExecutablePath().Should().Be(@"C:\repos\IFM\.artifacts\rust-ui\app\ifm-ui-rust.exe");
        ui.EnvironmentVariables["IFM_RUST_UI_MODE"].Should().Be("live");
        ui.WindowStyle.Should().Be(ProcessWindowStyle.Maximized);
        ui.ShutdownMode.Should().Be(ProcessShutdownMode.CloseMainWindow);
        ui.StartOrder.Should().BeGreaterThan(api.StartOrder);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("WinForms")]
    public void Default_selection_keeps_existing_winforms_definition(string? selection)
    {
        var options = Options();
        options.SelectDevelopmentUi("Development", selection, null);
        options.Processes[1].ExecutablePath.Should().Be("ui.exe");
        options.Processes[1].EnvironmentVariables.Should().NotContainKey("IFM_RUST_UI_MODE");
    }

    [Fact]
    public void Rust_selection_cannot_change_production_process_targets()
    {
        var options = Options();
        var action = () => options.SelectDevelopmentUi("Production", "Rust", @"C:\repos\IFM");
        action.Should().Throw<InvalidOperationException>();
        options.Processes[1].ExecutablePath.Should().Be("ui.exe");
    }
}
