using FluentAssertions;
using TomasAI.IFM.Application.ServerManager.SchedulerHost;

namespace TomasAI.IFM.Application.ServerManager.UnitTests;

public sealed class SchedulerHostOptionsTests
{
    [Fact]
    public void Validate_rejects_catalog_path_outside_deployment_root()
    {
        var root = Path.Combine(Path.GetTempPath(), "ifm-deployment");
        var options = CreateOptions(root);
        options.TaskCatalog[0].ExecutablePath = Path.Combine(Path.GetTempPath(), "outside.exe");

        var action = options.Validate;

        action.Should().Throw<InvalidOperationException>().WithMessage("*escapes deployment root*");
    }

    [Fact]
    public void ResolveExecutablePath_keeps_relative_catalog_path_below_root()
    {
        var root = Path.Combine(Path.GetTempPath(), "ifm-deployment");
        var options = CreateOptions(root);

        var resolved = options.TaskCatalog[0].ResolveExecutablePath(options);

        resolved.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase).Should().BeTrue();
        resolved.Should().EndWith(Path.Combine("tasks", "task.exe"));
    }

    private static SchedulerHostOptions CreateOptions(string root)
        => new()
        {
            DeploymentRoot = root,
            TaskRunRoot = Path.Combine(Path.GetTempPath(), "ifm-runs"),
            TaskCatalog =
            [
                new ScheduledTaskCatalogDefinition
                {
                    TaskKey = "task",
                    DisplayName = "Task",
                    WorkingDirectory = "tasks",
                    ExecutablePath = "task.exe",
                    MaximumRuntimeSeconds = 30
                }
            ]
        };
}
