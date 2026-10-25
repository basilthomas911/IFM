using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace TomasAI.IFM.Application.ServerManager;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1515",Justification="Preserve the existing public host, configuration and reflection contracts used by external test assemblies.")]

public sealed class ServerManagerOptions
{
    public int MaximumLogEntries { get; set; } = 5_000;

    public int ShutdownTimeoutSeconds { get; set; } = 10;

    public bool DevelopmentProcessOwnershipEnabled { get; set; }
    public bool ApiRecoveryRestartEnabled { get; set; }

    public SchedulerClientOptions Scheduler { get; set; } = new();
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA2227",Justification="Preserve the existing mutable configuration or test fixture contract.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1002",Justification="Preserve the existing mutable configuration or test fixture contract.")]

    public List<ManagedProcessDefinition> Processes { get; set; } = [];

    public TimeSpan ShutdownTimeout => TimeSpan.FromSeconds(ShutdownTimeoutSeconds);

    /// <summary>Selects the Development desktop while preserving managed API and scheduler lifecycle.</summary>
    public void SelectDevelopmentUi(string environment, string? selection, string? repositoryRoot)
    {
        ArgumentNullException.ThrowIfNull(environment);
        if (string.IsNullOrWhiteSpace(selection) || selection.Equals("WinForms", StringComparison.OrdinalIgnoreCase))
            return;
        var standalone = selection.Equals("eGui", StringComparison.OrdinalIgnoreCase);
        if (!standalone && !selection.Equals("Rust", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Unknown Development UI selection '{selection}'.");
        if (!environment.Equals("Development", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Rust desktop selection is available only in Development.");
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        var ui = Processes.Single(process => process.Key.Equals("ui", StringComparison.OrdinalIgnoreCase));
        ui.DisplayName = standalone ? "eGui Rust UI" : "Rust UI";
        ui.WorkingDirectory = Path.Combine(Path.GetFullPath(repositoryRoot), ".artifacts", standalone ? "standalone-rust-ui" : "rust-ui", "app");
        ui.ExecutablePath = standalone ? "TomasAI.IFM.UI.Rust.eGui.exe" : "ifm-ui-rust.exe";
        ui.Arguments.Clear();
        ui.EnvironmentVariables["DOTNET_ENVIRONMENT"] = "Development";
        // Only the hybrid loads a managed bridge; standalone reads its inherited client profile.
        if (standalone)
            ui.EnvironmentVariables.Remove("IFM_RUST_UI_MODE");
        else
            ui.EnvironmentVariables["IFM_RUST_UI_MODE"] = "live";
        ui.EnvironmentVariables["IFM_UI_START_MAXIMIZED"] = "true";
        ui.WindowStyle = ProcessWindowStyle.Maximized;
        ui.ShutdownMode = ProcessShutdownMode.CloseMainWindow;
    }

    public void Validate()
    {
        if (MaximumLogEntries <= 0)
        {
            throw new InvalidOperationException("ServerManager:MaximumLogEntries must be greater than zero.");
        }

        if (ShutdownTimeoutSeconds <= 0)
        {
            throw new InvalidOperationException("ServerManager:ShutdownTimeoutSeconds must be greater than zero.");
        }

        if (Processes.Count == 0)
        {
            throw new InvalidOperationException("ServerManager:Processes must define at least one process.");
        }

        Scheduler.Validate();

        var duplicateKey = Processes
            .GroupBy(process => process.Key, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicateKey is not null)
        {
            throw new InvalidOperationException($"ServerManager process key '{duplicateKey}' is duplicated.");
        }

        foreach (var process in Processes)
        {
            process.Validate();
        }
    }
}
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1515",Justification="Preserve the existing public host, configuration and reflection contracts used by external test assemblies.")]

public sealed class SchedulerClientOptions
{
    public bool Enabled { get; set; } = true;

    public string PipeName { get; set; } = "IFM.ServerManager.Scheduler.v1";

    public int ConnectTimeoutMilliseconds { get; set; } = 2_000;

    public int RefreshIntervalSeconds { get; set; } = 5;

    public void Validate()
    {
        if (!Enabled)
        {
            return;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(PipeName);
        if (ConnectTimeoutMilliseconds <= 0 || RefreshIntervalSeconds <= 0)
        {
            throw new InvalidOperationException("Scheduler client connection and refresh limits must be positive.");
        }
    }
}
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1515",Justification="Preserve the existing public host, configuration and reflection contracts used by external test assemblies.")]

public sealed class ManagedProcessDefinition
{
    public string Key { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string WorkingDirectory { get; set; } = string.Empty;

    public string ExecutablePath { get; set; } = string.Empty;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1002",Justification="Preserve the existing mutable configuration or test fixture contract.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA2227",Justification="Preserve the existing mutable configuration or test fixture contract.")]

    public List<string> Arguments { get; set; } = [];
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA2227",Justification="Preserve the existing mutable configuration or test fixture contract.")]

    public Dictionary<string, string> EnvironmentVariables { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public ProcessWindowStyle WindowStyle { get; set; } = ProcessWindowStyle.Hidden;

    public int StartOrder { get; set; }

    public bool Enabled { get; set; } = true;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1056",Justification="NATS clients and configuration consumers require the existing string endpoint contract.")]

    public string? ReadinessUri { get; set; }

    /// <summary>Gets or sets the scheduler dashboard pipe used to verify managed SchedulerHost startup.</summary>
    public string? ReadinessPipeName { get; set; }

    public int ReadinessTimeoutSeconds { get; set; } = 900;

    public int ReadinessPollIntervalMilliseconds { get; set; } = 500;

    public ProcessShutdownMode ShutdownMode { get; set; } = ProcessShutdownMode.CloseMainWindow;

    public string? ShutdownInput { get; set; }

    public string ResolveExecutablePath()
    {
        var expanded = Environment.ExpandEnvironmentVariables(ExecutablePath);
        return Path.IsPathRooted(expanded)
            ? Path.GetFullPath(expanded)
            : Path.GetFullPath(Path.Combine(ResolveWorkingDirectory(), expanded));
    }

    public string ResolveWorkingDirectory()
    {
        var expanded = Environment.ExpandEnvironmentVariables(WorkingDirectory);
        return Path.IsPathRooted(expanded)
            ? Path.GetFullPath(expanded)
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, expanded));
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Key))
        {
            throw new InvalidOperationException("Every ServerManager process requires a key.");
        }

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            throw new InvalidOperationException($"ServerManager process '{Key}' requires a display name.");
        }

        if (string.IsNullOrWhiteSpace(WorkingDirectory))
        {
            throw new InvalidOperationException($"ServerManager process '{Key}' requires a working directory.");
        }

        if (string.IsNullOrWhiteSpace(ExecutablePath))
        {
            throw new InvalidOperationException($"ServerManager process '{Key}' requires an executable path.");
        }

        foreach (var variable in EnvironmentVariables)
        {
            if (string.IsNullOrWhiteSpace(variable.Key) || variable.Key.Contains('=', StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"ServerManager process '{Key}' contains an invalid environment-variable name.");
            }
        }

        if (ShutdownMode == ProcessShutdownMode.StandardInput && string.IsNullOrWhiteSpace(ShutdownInput))
        {
            throw new InvalidOperationException(
                $"ServerManager process '{Key}' requires ShutdownInput when ShutdownMode is StandardInput.");
        }

        if (!string.IsNullOrWhiteSpace(ReadinessPipeName) && (ReadinessTimeoutSeconds <= 0 || ReadinessPollIntervalMilliseconds <= 0))
            throw new InvalidOperationException($"ServerManager process '{Key}' scheduler readiness limits must be positive.");

        if (!string.IsNullOrWhiteSpace(ReadinessUri))
        {
            if (!Uri.TryCreate(ReadinessUri, UriKind.Absolute, out var readinessUri)
                || readinessUri.Scheme is not ("http" or "https"))
            {
                throw new InvalidOperationException(
                    $"ServerManager process '{Key}' requires an absolute HTTP(S) ReadinessUri.");
            }

            if (ReadinessTimeoutSeconds <= 0 || ReadinessPollIntervalMilliseconds <= 0)
            {
                throw new InvalidOperationException(
                    $"ServerManager process '{Key}' readiness timeout and polling interval must be positive.");
            }
        }
    }
}
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1515",Justification="Preserve the existing public host, configuration and reflection contracts used by external test assemblies.")]

public enum ProcessShutdownMode
{
    None,
    CloseMainWindow,
    StandardInput
}
