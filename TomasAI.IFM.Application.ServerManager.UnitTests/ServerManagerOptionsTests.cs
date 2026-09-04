using FluentAssertions;
using System.Diagnostics;

namespace TomasAI.IFM.Application.ServerManager.UnitTests;

public sealed class ServerManagerOptionsTests
{
    [Fact]
    public void Validate_rejects_duplicate_process_keys_case_insensitively()
    {
        var options = ValidOptions();
        options.Processes.Add(new ManagedProcessDefinition
        {
            Key = "API",
            DisplayName = "Duplicate API",
            WorkingDirectory = "C:\\apps\\api-two",
            ExecutablePath = "api-two.exe"
        });

        var action = options.Validate;

        action.Should().Throw<InvalidOperationException>().WithMessage("*'API' is duplicated*");
    }

    [Fact]
    public void Validate_requires_shutdown_input_for_standard_input_mode()
    {
        var options = ValidOptions();
        options.Processes[0].ShutdownMode = ProcessShutdownMode.StandardInput;

        var action = options.Validate;

        action.Should().Throw<InvalidOperationException>().WithMessage("*requires ShutdownInput*");
    }

    [Fact]
    public void Validate_accepts_api_and_ui_process_definitions()
    {
        var options = ValidOptions();
        options.Processes.Add(new ManagedProcessDefinition
        {
            Key = "ui",
            DisplayName = "UI.Net",
            WorkingDirectory = "C:\\apps\\ui",
            ExecutablePath = "ui.exe",
            StartOrder = 20,
            WindowStyle = ProcessWindowStyle.Maximized,
            ShutdownMode = ProcessShutdownMode.CloseMainWindow
        });

        options.Invoking(value => value.Validate()).Should().NotThrow();
    }

    [Fact]
    public void Validate_accepts_absolute_http_readiness_probe()
    {
        var options = ValidOptions();
        options.Processes[0].ReadinessUri = "http://localhost:22543/health/ready";
        options.Processes[0].ReadinessTimeoutSeconds = 300;
        options.Processes[0].ReadinessPollIntervalMilliseconds = 500;

        options.Invoking(value => value.Validate()).Should().NotThrow();
    }

    [Fact]
    public void Validate_rejects_invalid_environment_variable_names()
    {
        var options = ValidOptions();
        options.Processes[0].EnvironmentVariables["INVALID=NAME"] = "value";

        options.Invoking(value => value.Validate()).Should().Throw<InvalidOperationException>()
            .WithMessage("*invalid environment-variable name*");
    }

    [Fact]
    public void Resolve_paths_expands_environment_variables_for_development_outputs()
    {
        const string variable = "IFM_SERVER_MANAGER_TEST_ROOT";
        var prior = Environment.GetEnvironmentVariable(variable);
        var root = Path.Combine(Path.GetTempPath(), $"ifm-{Guid.NewGuid():N}");
        try
        {
            Environment.SetEnvironmentVariable(variable, root);
            var definition = new ManagedProcessDefinition
            {
                Key = "api",
                DisplayName = "API",
                WorkingDirectory = $"%{variable}%\\api",
                ExecutablePath = "api.exe"
            };

            definition.ResolveWorkingDirectory().Should().Be(Path.Combine(root, "api"));
            definition.ResolveExecutablePath().Should().Be(Path.Combine(root, "api", "api.exe"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, prior);
        }
    }

    [Theory]
    [InlineData("health/ready")]
    [InlineData("file:///C:/ready.txt")]
    public void Validate_rejects_non_http_absolute_readiness_probe(string readinessUri)
    {
        var options = ValidOptions();
        options.Processes[0].ReadinessUri = readinessUri;

        options.Invoking(value => value.Validate()).Should().Throw<InvalidOperationException>()
            .WithMessage("*absolute HTTP(S) ReadinessUri*");
    }

    private static ServerManagerOptions ValidOptions()
        => new()
        {
            MaximumLogEntries = 100,
            ShutdownTimeoutSeconds = 5,
            Processes =
            [
                new ManagedProcessDefinition
                {
                    Key = "api",
                    DisplayName = "API Server",
                    WorkingDirectory = "C:\\apps\\api",
                    ExecutablePath = "api.exe",
                    StartOrder = 10,
                    ShutdownMode = ProcessShutdownMode.None
                }
            ]
        };
}
