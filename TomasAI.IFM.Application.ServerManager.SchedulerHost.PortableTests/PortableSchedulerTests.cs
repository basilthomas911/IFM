using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Quartz;
using TomasAI.IFM.Application.ServerManager.Contracts;
using TomasAI.IFM.Application.ServerManager.SchedulerHost;
using TomasAI.IFM.Application.ServerManager.TestProcess;

namespace TomasAI.IFM.Application.ServerManager.SchedulerHost.PortableTests;

public sealed class PortableSchedulerTests
{
    [Theory]
    [InlineData("0 1 17 ? * MON-FRI", "2026-03-06T22:01:00Z", "2026-03-09T21:01:00Z")]
    [InlineData("0 0 18 ? * SUN-THU", "2026-03-06T00:00:00Z", "2026-03-08T22:00:00Z")]
    [InlineData("0 1 17 ? * MON-FRI", "2026-10-30T21:01:00Z", "2026-11-02T22:01:00Z")]
    [InlineData("0 0 18 ? * SUN-THU", "2026-10-30T00:00:00Z", "2026-11-01T23:00:00Z")]
    public void Required_defaults_preserve_Eastern_time_across_DST(string cron, string after, string expected)
    {
        var iana = new CronExpression(cron) { TimeZone = SchedulerTimeZone.Resolve("America/New_York") };
        var windows = new CronExpression(cron) { TimeZone = SchedulerTimeZone.Resolve("Eastern Standard Time") };
        Assert.Equal(DateTimeOffset.Parse(expected), iana.GetNextValidTimeAfter(DateTimeOffset.Parse(after)));
        Assert.Equal(iana.GetNextValidTimeAfter(DateTimeOffset.Parse(after)), windows.GetNextValidTimeAfter(DateTimeOffset.Parse(after)));
    }

    [Fact]
    public void POSIX_cron_is_rejected_with_explicit_dialect_message()
    {
        var options = Options();
        options.TaskCatalog.Add(TaskDefinition());
        using var source = NpgsqlDataSource.Create("Host=localhost;Database=unused;Username=unused");
        var validation = new ScheduleValidationService(options, new TaskCatalogProvider(options, source));
        var result = validation.Validate(new ScheduleDefinitionInputDto(
            null, "Close", "", "helper", ScheduleKind.Cron, "1 17 * * 1-5",
            "America/New_York", SchedulerMisfirePolicy.DoNothing, 1));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("six fields"));
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task Child_process_receives_the_admitted_host_environment(string environment)
    {
        var options = Options();
        options.Environment = environment;
        var result = await RunAsync(options, TaskDefinition("--print-environment", "DOTNET_ENVIRONMENT"));
        Assert.True(result.State == ScheduledRunState.Succeeded, result.Detail);
        Assert.Contains($"environment:DOTNET_ENVIRONMENT={environment}", await File.ReadAllTextAsync(Path.Combine(options.TaskRunRoot, "stdout.log")));
    }

    [Fact]
    public async Task Task_output_is_captured_on_the_actual_platform()
    {
        var options = Options();
        var result = await RunAsync(options, TaskDefinition("--stdout-count", "25", "--stderr-count", "25"));
        Assert.True(result.State == ScheduledRunState.Succeeded, result.Detail);
        Assert.Equal(25, File.ReadLines(Path.Combine(options.TaskRunRoot, "stdout.log")).Count());
        Assert.Equal(25, File.ReadLines(Path.Combine(options.TaskRunRoot, "stderr.log")).Count());
    }

    [Fact]
    public async Task Noncooperative_task_times_out_with_a_bounded_termination()
    {
        var options = Options();
        var started = Stopwatch.StartNew();
        var result = await RunAsync(options, TaskDefinition("--delay-ms", "60000"));
        Assert.True(result.State == ScheduledRunState.TimedOut, result.Detail);
        Assert.True(started.Elapsed < TimeSpan.FromSeconds(15), result.Detail);
    }

    [Fact]
    public async Task Failed_started_callback_terminates_the_child_instead_of_orphaning_it()
    {
        var options = Options();
        int? child = null;
        var result = await RunAsync(options, TaskDefinition("--delay-ms", "60000"), (pid, _, _) =>
        {
            child = pid;
            throw new IOException("Injected receipt failure");
        });
        Assert.Equal(ScheduledRunState.Failed, result.State);
        Assert.NotNull(child);
        Assert.False(IsRunning(child!.Value));
    }

    [Fact]
    public async Task Output_limit_does_not_block_the_task()
    {
        var options = Options();
        options.MaximumOutputBytesPerStream = 300;
        var result = await RunAsync(options, TaskDefinition("--stdout-count", "500"));
        Assert.True(result.State == ScheduledRunState.Succeeded, result.Detail);
        Assert.True(result.StdoutTruncated);
    }

    [Fact]
    public void Deployment_root_comparison_respects_platform_case_rules()
    {
        if (!OperatingSystem.IsLinux()) return;
        var options = Options();
        options.DeploymentRoot = "/tmp/ifm-lower";
        var task = TaskDefinition();
        task.WorkingDirectory = "/tmp/IFM-LOWER";
        Assert.Throws<InvalidOperationException>(() => task.Validate(options));
    }

    [Fact]
    public async Task Abrupt_owner_death_terminates_its_contained_child()
    {
        var pidPath = Path.Combine(Path.GetTempPath(), "ifm-parent-death-" + Guid.NewGuid().ToString("N"));
        var helper = typeof(TestProcessMarker).Assembly.Location;
        var start = new ProcessStartInfo(OperatingSystem.IsWindows() ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe") : "/usr/share/dotnet/dotnet") { UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in new[] { helper, "--contain-child", pidPath }) start.ArgumentList.Add(argument);
        using var parent = Process.Start(start)!;
        int? child = null;
        try
        {
            var startup = Stopwatch.StartNew();
            while (!File.Exists(pidPath) && startup.Elapsed < TimeSpan.FromSeconds(10)) await Task.Delay(50);
            Assert.True(File.Exists(pidPath), "Containment owner did not report its child.");
            child = int.Parse(await File.ReadAllTextAsync(pidPath));
            parent.Kill(entireProcessTree: false);
            await parent.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            var termination = Stopwatch.StartNew();
            while (IsRunning(child.Value) && termination.Elapsed < TimeSpan.FromSeconds(5)) await Task.Delay(50);
            Assert.False(IsRunning(child.Value), "Child survived the abrupt loss of its containment owner.");
        }
        finally
        {
            if (!parent.HasExited) parent.Kill(true);
            if (child is { } pid && IsRunning(pid)) { using var remaining = Process.GetProcessById(pid); remaining.Kill(true); }
            File.Delete(pidPath);
        }
    }

    private static bool IsRunning(int pid)
    {
        try
        {
            if (OperatingSystem.IsLinux() && File.Exists($"/proc/{pid}/stat") && File.ReadAllText($"/proc/{pid}/stat").Split(')')[1].TrimStart().StartsWith("Z ")) return false;
            using var process = Process.GetProcessById(pid); return !process.HasExited;
        }
        catch (ArgumentException) { return false; }
    }

    private static SchedulerHostOptions Options() => new()
    {
        DeploymentRoot = Path.GetPathRoot(typeof(TestProcessMarker).Assembly.Location)!,
        TaskRunRoot = Path.Combine(Path.GetTempPath(), "ifm-portable-scheduler", Guid.NewGuid().ToString("N")),
        ProcessTerminationTimeoutSeconds = 3,
        OutputDrainTimeoutSeconds = 3
    };

    private static ScheduledTaskCatalogDefinition TaskDefinition(params string[] arguments)
    {
        var dotnet = OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe")
            : "/usr/share/dotnet/dotnet";
        var helper = typeof(TestProcessMarker).Assembly.Location;
        return new()
        {
            TaskKey = "helper", DisplayName = "Helper",
            WorkingDirectory = Path.GetDirectoryName(helper)!, ExecutablePath = dotnet,
            DefaultArguments = new[] { helper }.Concat(arguments).ToList(), MaximumRuntimeSeconds = 1,
            RiskClassification = SchedulerRiskClassification.MarketLifecycle
        };
    }

    private static Task<ScheduledProcessResult> RunAsync(SchedulerHostOptions options, ScheduledTaskCatalogDefinition task,
        Func<int, DateTimeOffset, CancellationToken, Task>? started = null)
        => new ScheduledProcessRunner(options, NullLogger<ScheduledProcessRunner>.Instance).RunAsync(
            task, new ScheduledProcessIdentity(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                ScheduledRunOrigin.Manual, DateTimeOffset.UtcNow),
            Path.Combine(options.TaskRunRoot, "stdout.log"), Path.Combine(options.TaskRunRoot, "stderr.log"),
            started ?? ((_, _, _) => Task.CompletedTask), CancellationToken.None);
}
