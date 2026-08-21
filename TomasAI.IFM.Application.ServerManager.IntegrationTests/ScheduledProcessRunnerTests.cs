using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.ServerManager.Contracts;
using TomasAI.IFM.Application.ServerManager.SchedulerHost;
using TomasAI.IFM.Application.ServerManager.TestProcess;

namespace TomasAI.IFM.Application.ServerManager.IntegrationTests;

public sealed class ScheduledProcessRunnerTests
{
    [Fact]
    public async Task Assigns_job_object_and_captures_both_output_streams()
    {
        var dotnet = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "dotnet",
            "dotnet.exe");
        var helper = typeof(TestProcessMarker).Assembly.Location;
        var runRoot = Path.Combine(Path.GetTempPath(), "ifm-job-object-tests", Guid.NewGuid().ToString("N"));
        var options = new SchedulerHostOptions
        {
            Environment = "Development",
            DeploymentRoot = Path.GetPathRoot(dotnet)!,
            TaskRunRoot = runRoot
        };
        var task = new ScheduledTaskCatalogDefinition
        {
            TaskKey = "helper",
            DisplayName = "Helper",
            WorkingDirectory = Path.GetDirectoryName(helper)!,
            ExecutablePath = dotnet,
            DefaultArguments = [helper, "--stdout-count", "25", "--stderr-count", "25"],
            MaximumRuntimeSeconds = 10,
            SuccessExitCodes = [0]
        };
        task.Validate(options);
        var stdout = Path.Combine(runRoot, "stdout.log");
        var stderr = Path.Combine(runRoot, "stderr.log");
        var started = false;
        var runner = new ScheduledProcessRunner(options, NullLogger<ScheduledProcessRunner>.Instance);

        var result = await runner.RunAsync(
            task,
            new ScheduledProcessIdentity(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                ScheduledRunOrigin.Manual,
                DateTimeOffset.UtcNow),
            stdout,
            stderr,
            (processId, startedAt, cancellationToken) =>
            {
                processId.Should().BePositive();
                startedAt.Should().BeBefore(DateTimeOffset.UtcNow.AddSeconds(1));
                started = true;
                return Task.CompletedTask;
            },
            CancellationToken.None);

        result.State.Should().Be(ScheduledRunState.Succeeded);
        started.Should().BeTrue();
        File.ReadLines(stdout).Should().HaveCount(25);
        File.ReadLines(stderr).Should().HaveCount(25);
    }

    [Fact]
    public async Task Bounds_output_while_continuing_to_drain_the_child_process()
    {
        var (dotnet, helper, root) = Paths();
        var options = new SchedulerHostOptions
        {
            Environment = "Development",
            DeploymentRoot = Path.GetPathRoot(dotnet)!,
            TaskRunRoot = root,
            MaximumOutputLineCharacters = 12,
            MaximumOutputBytesPerStream = 300
        };
        var task = CreateTask(dotnet, helper, [helper, "--stdout-count", "100", "--stderr-count", "100"]);
        var runner = new ScheduledProcessRunner(options, NullLogger<ScheduledProcessRunner>.Instance);
        var result = await runner.RunAsync(
            task,
            Identity(),
            Path.Combine(root, "stdout.log"),
            Path.Combine(root, "stderr.log"),
            (_, _, _) => Task.CompletedTask,
            CancellationToken.None);

        result.State.Should().Be(ScheduledRunState.Succeeded);
        result.StdoutTruncated.Should().BeTrue();
        result.StderrTruncated.Should().BeTrue();
        File.ReadAllText(Path.Combine(root, "stdout.log")).Should().Contain("OUTPUT TRUNCATED");
    }

    [Fact]
    public async Task Named_pipe_cancellation_stops_cooperating_task_without_force_termination()
    {
        var (dotnet, helper, root) = Paths();
        var options = new SchedulerHostOptions
        {
            Environment = "Development",
            DeploymentRoot = Path.GetPathRoot(dotnet)!,
            TaskRunRoot = root
        };
        var task = CreateTask(dotnet, helper, [helper, "--wait-for-control-pipe", "true"]);
        task.GracefulStopMode = ScheduledTaskStopMode.NamedPipe;
        using var cancellation = new CancellationTokenSource();
        var runner = new ScheduledProcessRunner(options, NullLogger<ScheduledProcessRunner>.Instance);
        var result = await runner.RunAsync(
            task,
            Identity(),
            Path.Combine(root, "stdout.log"),
            Path.Combine(root, "stderr.log"),
            (_, _, _) =>
            {
                cancellation.CancelAfter(300);
                return Task.CompletedTask;
            },
            cancellation.Token);

        result.State.Should().Be(ScheduledRunState.Cancelled);
        File.ReadAllText(Path.Combine(root, "stdout.log")).Should().Contain("control-pipe-cancelled");
    }

    private static (string Dotnet, string Helper, string Root) Paths()
    {
        var dotnet = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe");
        var helper = typeof(TestProcessMarker).Assembly.Location;
        var root = Path.Combine(Path.GetTempPath(), "ifm-scheduler-runner", Guid.NewGuid().ToString("N"));
        return (dotnet, helper, root);
    }

    private static ScheduledTaskCatalogDefinition CreateTask(string dotnet, string helper, List<string> arguments)
        => new()
        {
            TaskKey = "helper",
            DisplayName = "Helper",
            WorkingDirectory = Path.GetDirectoryName(helper)!,
            ExecutablePath = dotnet,
            DefaultArguments = arguments,
            MaximumRuntimeSeconds = 10,
            SuccessExitCodes = [0]
        };

    private static ScheduledProcessIdentity Identity()
    {
        var runId = Guid.NewGuid();
        return new ScheduledProcessIdentity(
            runId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            ScheduledRunOrigin.Manual,
            DateTimeOffset.UtcNow,
            $"IFM.TestControl.{runId:N}");
    }
}
