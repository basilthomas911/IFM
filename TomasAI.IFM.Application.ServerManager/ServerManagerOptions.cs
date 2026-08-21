using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace TomasAI.IFM.Application.ServerManager;

public sealed class ServerManagerOptions
{
    public int MaximumLogEntries { get; set; } = 5_000;

    public int ShutdownTimeoutSeconds { get; set; } = 10;

    public SchedulerClientOptions Scheduler { get; set; } = new();

    public List<ManagedProcessDefinition> Processes { get; set; } = [];

    public TimeSpan ShutdownTimeout => TimeSpan.FromSeconds(ShutdownTimeoutSeconds);

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

public sealed class ManagedProcessDefinition
{
    public string Key { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string WorkingDirectory { get; set; } = string.Empty;

    public string ExecutablePath { get; set; } = string.Empty;

    public List<string> Arguments { get; set; } = [];

    public Dictionary<string, string> EnvironmentVariables { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public ProcessWindowStyle WindowStyle { get; set; } = ProcessWindowStyle.Hidden;

    public int StartOrder { get; set; }

    public bool Enabled { get; set; } = true;

    public string? ReadinessUri { get; set; }

    public int ReadinessTimeoutSeconds { get; set; } = 300;

    public int ReadinessPollIntervalMilliseconds { get; set; } = 500;

    public ProcessShutdownMode ShutdownMode { get; set; } = ProcessShutdownMode.CloseMainWindow;

    public string? ShutdownInput { get; set; }

    public string ResolveExecutablePath()
        => Path.IsPathRooted(ExecutablePath)
            ? Path.GetFullPath(ExecutablePath)
            : Path.GetFullPath(Path.Combine(ResolveWorkingDirectory(), ExecutablePath));

    public string ResolveWorkingDirectory()
        => Path.IsPathRooted(WorkingDirectory)
            ? Path.GetFullPath(WorkingDirectory)
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, WorkingDirectory));

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
            if (string.IsNullOrWhiteSpace(variable.Key) || variable.Key.Contains('='))
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

public enum ProcessShutdownMode
{
    None,
    CloseMainWindow,
    StandardInput
}
