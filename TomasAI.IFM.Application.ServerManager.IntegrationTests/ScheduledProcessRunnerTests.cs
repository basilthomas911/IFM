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
}
