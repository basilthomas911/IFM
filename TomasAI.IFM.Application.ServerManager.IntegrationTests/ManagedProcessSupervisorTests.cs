using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using FluentAssertions;
using TomasAI.IFM.Application.ServerManager.TestProcess;

namespace TomasAI.IFM.Application.ServerManager.IntegrationTests;

public sealed class ManagedProcessSupervisorTests
{
    [Fact]
    public async Task Dispose_cancels_owned_startup_readiness_then_gracefully_joins_the_real_child()
    {
        var logs=new ConcurrentQueue<ManagedProcessLogEntry>();var definition=CreateDefinition("api",ProcessShutdownMode.StandardInput,"--wait-for-shutdown","stop");
        definition.ReadinessUri=$"http://127.0.0.1:{ReserveAvailablePort()}/never-ready";definition.ReadinessTimeoutSeconds=900;
        await using var supervisor=new ManagedProcessSupervisor([definition],TimeSpan.FromSeconds(2),logs.Enqueue);
        var startup=supervisor.StartAllAsync();
        await WaitUntilAsync(()=>supervisor.RunningProcesses.Count==1,TimeSpan.FromSeconds(5));
        var child=Assert.Single(supervisor.RunningProcesses);var began=Stopwatch.StartNew();
        await supervisor.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>startup);
        Assert.Empty(supervisor.RunningProcessKeys);Assert.True(began.Elapsed<TimeSpan.FromSeconds(5));
        Assert.Contains(logs,row=>row.Message=="graceful-shutdown");
        Assert.DoesNotContain(logs,row=>row.Message.Contains("forc",StringComparison.OrdinalIgnoreCase));
        Assert.False(IsLiveProcess(child.ProcessId));
        static bool IsLiveProcess(int pid){try{using var process=Process.GetProcessById(pid);return !process.HasExited;}catch(ArgumentException){return false;}}
    }

    [Fact]
    public async Task Capture_profile_stops_and_joins_native_before_stopping_the_API()
    {
        var previous=Environment.GetEnvironmentVariable("IFM_SESSION_CAPTURE_BOUNDARY_ENABLED");
        Environment.SetEnvironmentVariable("IFM_SESSION_CAPTURE_BOUNDARY_ENABLED","1");
        try
        {
            var logs=new ConcurrentQueue<ManagedProcessLogEntry>();
            var definitions=new[]{"market-data","scheduler","api","ui"}.Select((key,index)=>
            {
                var definition=key=="market-data"?CreateDefinition(key,ProcessShutdownMode.StandardInput,"--wait-for-shutdown","stop","--delay-ms","2100")
                    :CreateDefinition(key,ProcessShutdownMode.StandardInput,"--wait-for-shutdown","stop");
                definition.StartOrder=index;return definition;
            }).ToArray();
            await using var supervisor=new ManagedProcessSupervisor(definitions,TimeSpan.FromSeconds(2),logs.Enqueue);
            await supervisor.StartAllAsync();await supervisor.StopAllAsync();
            Assert.Empty(supervisor.RunningProcessKeys);
            Assert.Equal(new[]{"ui","scheduler","market-data","api"},logs.Where(row=>row.Message=="Graceful shutdown requested.").Select(row=>row.ProcessKey));
            Assert.DoesNotContain(logs,row=>row.Message.Contains("forc",StringComparison.OrdinalIgnoreCase));
        }
        finally{Environment.SetEnvironmentVariable("IFM_SESSION_CAPTURE_BOUNDARY_ENABLED",previous);}
    }

    [Fact]
    public async Task Capture_hold_times_out_behind_incomplete_startup_without_acquiring_a_lease()
    {
        var logs=new ConcurrentQueue<ManagedProcessLogEntry>();var definition=CreateDefinition("api",ProcessShutdownMode.StandardInput,"--wait-for-shutdown","stop");
        definition.ReadinessUri=$"http://127.0.0.1:{ReserveAvailablePort()}/not-ready";definition.ReadinessTimeoutSeconds=60;
        await using var supervisor=new ManagedProcessSupervisor([definition],TimeSpan.FromSeconds(2),logs.Enqueue);
        using var stop=new CancellationTokenSource();var startup=supervisor.StartAllAsync(stop.Token);
        await WaitUntilAsync(()=>supervisor.RunningProcesses.Count==1,TimeSpan.FromSeconds(5));
        var began=Stopwatch.StartNew();
        await Assert.ThrowsAsync<TimeoutException>(()=>supervisor.HoldCaptureAsync(Guid.NewGuid().ToString("N"),CancellationToken.None));
        Assert.InRange(began.Elapsed.TotalSeconds,9,15);Assert.Equal("Open",supervisor.CaptureStatus().State);
        stop.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>startup);
    }

    [Fact]
    public async Task Capture_hold_stops_only_real_ui_scheduler_then_native_without_killing_and_parks_api_recovery()
    {
        var logs=new ConcurrentQueue<ManagedProcessLogEntry>();
        var definitions=new[]{"api","ui","scheduler","market-data"}.Select(key=>CreateDefinition(key,ProcessShutdownMode.StandardInput,"--wait-for-shutdown","stop")).ToArray();
        await using var supervisor=new ManagedProcessSupervisor(definitions,TimeSpan.FromMilliseconds(20),logs.Enqueue);
        await supervisor.StartAllAsync();var original=supervisor.RunningProcesses.ToDictionary(x=>x.ProcessKey);
        var lease=Guid.NewGuid().ToString("N");var held=await supervisor.HoldCaptureAsync(lease,CancellationToken.None);
        Assert.Equal("Held",held.State);Assert.False(held.ForcedStop);Assert.Equal(2,held.StoppedWriters.Count);
        Assert.Equal(new[]{"api","market-data"},supervisor.RunningProcessKeys.Order());
        await Assert.ThrowsAsync<InvalidOperationException>(()=>supervisor.RestartAllAsync());
        using(var cancellation=new CancellationTokenSource())
        {
            var recovery=supervisor.RestartApiAsync(original["api"].ProcessId,original["api"].StartedAtUtc.UtcDateTime,cancellation.Token);
            await Task.Delay(50);Assert.False(recovery.IsCompleted);cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>recovery);
        }
        Assert.Equal(original["api"].ProcessId,Assert.Single(supervisor.RunningProcesses,x=>x.ProcessKey=="api").ProcessId);
        await supervisor.StopCaptureNativeAsync(lease,CancellationToken.None);
        Assert.Equal("api",Assert.Single(supervisor.RunningProcessKeys));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>supervisor.ReleaseCaptureAsync(Guid.NewGuid().ToString("N"),CancellationToken.None));
        await supervisor.ReleaseCaptureAsync(lease,CancellationToken.None);
        Assert.Equal(4,supervisor.RunningProcessKeys.Count);Assert.Equal("Open",supervisor.CaptureStatus().State);
        Assert.DoesNotContain(logs,x=>x.Message.Contains("forc",StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Capture_without_graceful_channel_refuses_and_keeps_actual_writer_alive()
    {
        var logs=new ConcurrentQueue<ManagedProcessLogEntry>();
        var scheduler=CreateDefinition("scheduler",ProcessShutdownMode.None,"--wait-for-shutdown","stop");
        await using var supervisor=new ManagedProcessSupervisor([scheduler],TimeSpan.FromMilliseconds(20),logs.Enqueue);
        await supervisor.StartAllAsync();var original=Assert.Single(supervisor.RunningProcesses);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>supervisor.HoldCaptureAsync(Guid.NewGuid().ToString("N"),CancellationToken.None));
        Assert.Equal("Held",supervisor.CaptureStatus().State);Assert.Equal(original.ProcessId,Assert.Single(supervisor.RunningProcesses).ProcessId);
        Assert.DoesNotContain(logs,x=>x.Message.Contains("forc",StringComparison.OrdinalIgnoreCase));
    }

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
    public async Task Passes_configured_environment_variables_to_the_child_process()
    {
        var logs = new ConcurrentQueue<ManagedProcessLogEntry>();
        var definition = CreateDefinition(
            "environment",
            ProcessShutdownMode.None,
            "--print-environment", "IFM_SERVER_MANAGER_ENVIRONMENT_TEST");
        definition.EnvironmentVariables["IFM_SERVER_MANAGER_ENVIRONMENT_TEST"] = "configured-value";
        await using var supervisor = new ManagedProcessSupervisor(
            [definition],
            TimeSpan.FromSeconds(2),
            logs.Enqueue);

        await supervisor.StartAllAsync();
        await supervisor.WaitForExitAsync("environment").WaitAsync(TimeSpan.FromSeconds(15));

        logs.Should().Contain(entry => entry.Stream == ManagedProcessLogStream.StandardOutput
            && entry.Message == "environment:IFM_SERVER_MANAGER_ENVIRONMENT_TEST=configured-value");
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
    public async Task Readiness_gate_delays_the_next_process_until_the_endpoint_is_healthy()
    {
        var logs = new ConcurrentQueue<ManagedProcessLogEntry>();
        var readinessPort = ReserveAvailablePort();
        var first = CreateDefinition(
            "first",
            ProcessShutdownMode.StandardInput,
            "--wait-for-shutdown", "stop");
        first.ReadinessUri = $"http://127.0.0.1:{readinessPort}/health/ready";
        first.ReadinessTimeoutSeconds = 10;
        first.ReadinessPollIntervalMilliseconds = 50;
        var second = CreateDefinition(
            "second",
            ProcessShutdownMode.StandardInput,
            "--wait-for-shutdown", "stop");
        second.StartOrder = 20;
        second.StartupDependencies = ["first"];

        await using var supervisor = new ManagedProcessSupervisor(
            [first, second],
            TimeSpan.FromSeconds(2),
            logs.Enqueue);

        var start = supervisor.StartAllAsync();
        await WaitUntilAsync(
            () => logs.Any(entry => entry.ProcessKey == "first"
                && entry.Message.StartsWith("Started process")),
            TimeSpan.FromSeconds(5));
        await Task.Delay(200);
        logs.Count(entry => entry.Message.StartsWith("Started process")).Should().Be(1);

        var readinessResponse = ServeReadinessAsync(readinessPort);
        await start.WaitAsync(TimeSpan.FromSeconds(15));
        await readinessResponse.WaitAsync(TimeSpan.FromSeconds(5));

        logs.Should().Contain(entry => entry.ProcessKey == "first"
            && entry.Message == "Readiness confirmed with HTTP 200.");
        logs.Count(entry => entry.Message.StartsWith("Started process")).Should().Be(2);
        await supervisor.StopAllAsync();
    }

    [Fact]
    public async Task Readiness_timeout_blocks_dependents_and_keeps_independent_processes_running()
    {
        var logs = new ConcurrentQueue<ManagedProcessLogEntry>();
        var first = CreateDefinition(
            "first",
            ProcessShutdownMode.StandardInput,
            "--wait-for-shutdown", "stop");
        first.ReadinessUri = $"http://127.0.0.1:{ReserveAvailablePort()}/health/ready";
        first.ReadinessTimeoutSeconds = 1;
        first.ReadinessPollIntervalMilliseconds = 50;
        var second = CreateDefinition(
            "second",
            ProcessShutdownMode.StandardInput,
            "--wait-for-shutdown", "stop");
        second.StartOrder = 20;
        second.StartupDependencies = ["first"];

        var independent = CreateDefinition("independent", ProcessShutdownMode.StandardInput, "--wait-for-shutdown", "stop");
        await using var supervisor = new ManagedProcessSupervisor(
            [first, second, independent],
            TimeSpan.FromSeconds(2),
            logs.Enqueue);

        var start = () => supervisor.StartAllAsync();

        await start.Should().ThrowAsync<AggregateException>();
        logs.Should().Contain(entry => entry.ProcessKey == "second" && entry.Message.Contains("startup blocked"));
        logs.Should().Contain(entry => entry.ProcessKey == "independent" && entry.Message == "Startup ready.");
        supervisor.RunningProcessKeys.Should().Contain("independent").And.NotContain("second");
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

        await Assert.ThrowsAsync<AggregateException>(() => supervisor.StartAllAsync());
        await supervisor.WaitForExitAsync("missing");

        logs.Should().Contain(entry => entry.Message.StartsWith("Start failed:")
            && entry.Message.Contains("Managed executable was not found"));
        supervisor.RunningProcessKeys.Should().BeEmpty();
    }

    [Fact]
    public async Task Closing_development_job_terminates_assigned_process()
    {
        var definition = CreateDefinition(
            "job-child",
            ProcessShutdownMode.None,
            "--delay-ms", "30000");
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = definition.ResolveExecutablePath(),
                WorkingDirectory = definition.ResolveWorkingDirectory(),
                UseShellExecute = false
            }
        };
        foreach (var argument in definition.Arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start().Should().BeTrue();
        using (var job = new WindowsKillOnCloseJob())
        {
            job.Assign(process);
        }

        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        process.HasExited.Should().BeTrue();
    }

    [Fact]
    public async Task Reports_running_process_identity_when_children_start_and_exit()
    {
        var logs = new ConcurrentQueue<ManagedProcessLogEntry>();
        var snapshots = new ConcurrentQueue<IReadOnlyCollection<ManagedProcessIdentity>>();
        var definition = CreateDefinition(
            "identity",
            ProcessShutdownMode.StandardInput,
            "--wait-for-shutdown", "stop");
        await using var supervisor = new ManagedProcessSupervisor(
            [definition],
            TimeSpan.FromSeconds(2),
            logs.Enqueue,
            runningProcessesChanged: snapshots.Enqueue);

        await supervisor.StartAllAsync();
        snapshots.Should().Contain(snapshot => snapshot.Any(identity =>
            identity.ProcessKey == "identity"
            && identity.ProcessId > 0
            && identity.ExecutablePath == definition.ResolveExecutablePath()));

        await supervisor.StopAllAsync();
        snapshots.Last().Should().BeEmpty();
    }

    private static ManagedProcessSupervisor CreateSupervisor(
        ConcurrentQueue<ManagedProcessLogEntry> logs,
        ProcessShutdownMode shutdownMode,
        params string[] arguments)
    {
        var definition = CreateDefinition("test", shutdownMode, arguments);

        return new ManagedProcessSupervisor([definition], TimeSpan.FromSeconds(2), logs.Enqueue);
    }

    private static ManagedProcessDefinition CreateDefinition(
        string key,
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
            Key = key,
            DisplayName = $"{key} Process",
            WorkingDirectory = Path.GetDirectoryName(helperAssembly)!,
            ExecutablePath = dotnet,
            Arguments = [helperAssembly, .. arguments],
            ShutdownMode = shutdownMode,
            ShutdownInput = shutdownMode == ProcessShutdownMode.StandardInput ? "stop" : null
        };

        return definition;
    }

    private static int ReserveAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task ServeReadinessAsync(int port)
    {
        var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        try
        {
            using var client = await listener.AcceptTcpClientAsync();
            await using var stream = client.GetStream();
            using var reader = new StreamReader(
                stream,
                Encoding.ASCII,
                detectEncodingFromByteOrderMarks: false,
                leaveOpen: true);
            while (!string.IsNullOrEmpty(await reader.ReadLineAsync()))
            {
            }

            var response = Encoding.ASCII.GetBytes(
                "HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nOK");
            await stream.WriteAsync(response);
            await stream.FlushAsync();
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        while (!condition())
        {
            await Task.Delay(25, cancellation.Token);
        }
    }
}
