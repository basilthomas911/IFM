using System.Collections.Concurrent;
using System.Diagnostics;
using FluentAssertions;
using TomasAI.IFM.Application.ServerManager.TestProcess;

namespace TomasAI.IFM.Application.ServerManager.IntegrationTests;

public sealed class ManagedProcessSupervisorTests
{
    [Fact]
    public async Task Captures_high_volume_stdout_stderr_and_nonzero_exit_without_deadlock()
    {
        var logs = new ConcurrentQueue<ManagedProcessLogEntry>();
        await using var supervisor = CreateSupervisor(
            logs,
            ProcessShutdownMode.None,
            "--stdout-count", "1000",
            "--stderr-count", "1000",
            "--exit-code", "7");

        await supervisor.StartAllAsync();
        await supervisor.WaitForExitAsync("test").WaitAsync(TimeSpan.FromSeconds(15));

        logs.Count(entry => entry.Stream == ManagedProcessLogStream.StandardOutput).Should().Be(1000);
        logs.Count(entry => entry.Stream == ManagedProcessLogStream.StandardError).Should().Be(1000);
        logs.Should().Contain(entry => entry.Stream == ManagedProcessLogStream.Lifecycle
            && entry.Message == "Exited with code 7.");
    }

    [Fact]
    public async Task Uses_standard_input_for_graceful_shutdown()
    {
        var logs = new ConcurrentQueue<ManagedProcessLogEntry>();
        await using var supervisor = CreateSupervisor(
            logs,
            ProcessShutdownMode.StandardInput,
            "--wait-for-shutdown", "stop");

        await supervisor.StartAllAsync();
        await supervisor.StopAllAsync();

        logs.Should().Contain(entry => entry.Message == "Graceful shutdown requested.");
        logs.Should().Contain(entry => entry.Stream == ManagedProcessLogStream.StandardOutput
            && entry.Message == "graceful-shutdown");
        logs.Should().NotContain(entry => entry.Message.Contains("Forced process-tree termination requested"));
        supervisor.RunningProcessKeys.Should().BeEmpty();
    }

    [Fact]
    public async Task Records_forced_fallback_when_no_graceful_channel_exists()
    {
        var logs = new ConcurrentQueue<ManagedProcessLogEntry>();
        await using var supervisor = CreateSupervisor(
            logs,
            ProcessShutdownMode.None,
            "--delay-ms", "30000");

        await supervisor.StartAllAsync();
        await supervisor.StopAllAsync().WaitAsync(TimeSpan.FromSeconds(5));

        logs.Should().Contain(entry => entry.Message.StartsWith("No graceful shutdown channel was available"));
        logs.Should().Contain(entry => entry.Message == "Forced process-tree termination requested.");
        supervisor.RunningProcessKeys.Should().BeEmpty();
    }

    [Fact]
    public async Task Restart_stops_the_owned_process_before_starting_a_replacement()
    {
        var logs = new ConcurrentQueue<ManagedProcessLogEntry>();
        await using var supervisor = CreateSupervisor(
            logs,
            ProcessShutdownMode.StandardInput,
            "--wait-for-shutdown", "stop");

        await supervisor.StartAllAsync();
        await supervisor.RestartAllAsync();
        await supervisor.StopAllAsync();

        logs.Count(entry => entry.Stream == ManagedProcessLogStream.Lifecycle
            && entry.Message.StartsWith("Started process")).Should().Be(2);
        logs.Count(entry => entry.Stream == ManagedProcessLogStream.StandardOutput
            && entry.Message == "graceful-shutdown").Should().Be(2);
        supervisor.RunningProcessKeys.Should().BeEmpty();
    }

    [Fact]
    public async Task Missing_executable_is_reported_without_abandoning_lifecycle_control()
    {
        var logs = new ConcurrentQueue<ManagedProcessLogEntry>();
        var definition = new ManagedProcessDefinition
        {
            Key = "missing",
            DisplayName = "Missing",
            WorkingDirectory = Path.GetTempPath(),
            ExecutablePath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.exe"),
            ShutdownMode = ProcessShutdownMode.None
        };
        await using var supervisor = new ManagedProcessSupervisor([definition], TimeSpan.FromSeconds(1), logs.Enqueue);

        await supervisor.StartAllAsync();
        await supervisor.WaitForExitAsync("missing");

        logs.Should().Contain(entry => entry.Message.StartsWith("Start failed:")
            && entry.Message.Contains("Managed executable was not found"));
        supervisor.RunningProcessKeys.Should().BeEmpty();
    }

    private static ManagedProcessSupervisor CreateSupervisor(
        ConcurrentQueue<ManagedProcessLogEntry> logs,
        ProcessShutdownMode shutdownMode,
        params string[] arguments)
    {
        var dotnet = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "dotnet",
            "dotnet.exe");
        File.Exists(dotnet).Should().BeTrue("the .NET host is required for the helper-process integration tests");

        var helperAssembly = typeof(TestProcessMarker).Assembly.Location;
        var definition = new ManagedProcessDefinition
        {
            Key = "test",
            DisplayName = "Test Process",
            WorkingDirectory = Path.GetDirectoryName(helperAssembly)!,
            ExecutablePath = dotnet,
            Arguments = [helperAssembly, .. arguments],
            ShutdownMode = shutdownMode,
            ShutdownInput = shutdownMode == ProcessShutdownMode.StandardInput ? "stop" : null
        };

        return new ManagedProcessSupervisor([definition], TimeSpan.FromSeconds(2), logs.Enqueue);
    }
}
