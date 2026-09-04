using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TomasAI.IFM.Application.ServerManager;

public sealed class DevelopmentProcessSession : IDisposable
{
    public const string SessionEnvironmentVariable = "IFM_DEVELOPMENT_SESSION_ID";
    public const string RoleEnvironmentVariable = "IFM_DEVELOPMENT_PROCESS_ROLE";
    public const string ControlPipeName = "IFM.ServerManager.Development.v1";

    private const string MutexName = "Local\\TomasAI.IFM.ServerManager.Development.v1";
    private readonly EventWaitHandle _singleton;
    private readonly string _sessionFilePath;
    private readonly object _recordSync = new();
    private bool _disposed;

    public DevelopmentProcessSession(string? sessionFilePath = null, string? mutexName = null)
    {
        _sessionFilePath = sessionFilePath ?? SessionFilePath;
        _singleton = new EventWaitHandle(
            initialState: false,
            EventResetMode.ManualReset,
            mutexName ?? MutexName,
            out var createdNew);
        if (!createdNew)
        {
            _singleton.Dispose();
            throw new InvalidOperationException(
                "Another IFM Development Server Manager session is already active.");
        }

        SessionId = Guid.NewGuid().ToString("N");
    }

    public string SessionId { get; }

    public static string SessionFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TomasAI",
        "IFM",
        "Development",
        "server-manager-session.json");

    public void MarkDefinitions(IEnumerable<ManagedProcessDefinition> definitions)
    {
        foreach (var definition in definitions)
        {
            definition.EnvironmentVariables[SessionEnvironmentVariable] = SessionId;
            definition.EnvironmentVariables[RoleEnvironmentVariable] = definition.Key;
        }
    }

    public async Task ReconcilePreviousSessionAsync(
        Action<string> writeLog,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(writeLog);
        if (!File.Exists(_sessionFilePath))
        {
            return;
        }

        DevelopmentSessionRecord? record;
        try
        {
            await using var stream = File.OpenRead(_sessionFilePath);
            record = await JsonSerializer.DeserializeAsync<DevelopmentSessionRecord>(
                stream,
                cancellationToken: cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            throw new InvalidOperationException(
                $"The IFM Development session record '{_sessionFilePath}' is unreadable. "
                + "No process was terminated; remove or inspect the record manually.",
                exception);
        }

        if (record is null || record.SchemaVersion != DevelopmentSessionRecord.CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"The IFM Development session record '{_sessionFilePath}' has an unsupported format. "
                + "No process was terminated.");
        }

        foreach (var child in record.Children.OrderByDescending(value => value.StartOrder))
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var process = TryGetProcess(child.ProcessId);
            if (process is null)
            {
                continue;
            }

            ValidateIdentity(process, child);
            writeLog(
                $"Reconciling previously owned Development process '{child.ProcessKey}' "
                + $"(PID {child.ProcessId}).");
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(cancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
        }

        Clear();
        writeLog("Previous IFM Development session reconciliation completed.");
    }

    public void Record(IEnumerable<ManagedProcessIdentity> children)
    {
        lock (_recordSync)
        {
            using var process = Process.GetCurrentProcess();
            var record = new DevelopmentSessionRecord(
                DevelopmentSessionRecord.CurrentSchemaVersion,
                SessionId,
                process.Id,
                new DateTimeOffset(process.StartTime.ToUniversalTime()),
                Environment.ProcessPath ?? string.Empty,
                children.Select(child => new DevelopmentChildProcessRecord(
                    child.ProcessKey,
                    child.ProcessId,
                    child.StartedAtUtc,
                    child.ExecutablePath,
                    child.StartOrder)).ToArray());
            var directory = Path.GetDirectoryName(_sessionFilePath)!;
            Directory.CreateDirectory(directory);
            var temporaryPath = Path.Combine(directory, $"session-{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllText(
                    temporaryPath,
                    JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true }));
                File.Move(temporaryPath, _sessionFilePath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }
    }

    public void Clear()
    {
        lock (_recordSync)
        {
            if (File.Exists(_sessionFilePath))
            {
                File.Delete(_sessionFilePath);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _singleton.Dispose();
    }

    private static Process? TryGetProcess(int processId)
    {
        try
        {
            return Process.GetProcessById(processId);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static void ValidateIdentity(Process process, DevelopmentChildProcessRecord expected)
    {
        DateTimeOffset actualStart;
        string actualPath;
        try
        {
            actualStart = new DateTimeOffset(process.StartTime.ToUniversalTime());
            actualPath = Path.GetFullPath(process.MainModule?.FileName
                ?? throw new InvalidOperationException("The executable path is unavailable."));
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            throw new InvalidOperationException(
                $"Could not validate recorded Development process PID {expected.ProcessId}. "
                + "No process was terminated.",
                exception);
        }

        var expectedPath = Path.GetFullPath(expected.ExecutablePath);
        var sameStart = Math.Abs((actualStart - expected.StartedAtUtc).TotalSeconds) < 1;
        var samePath = string.Equals(actualPath, expectedPath, StringComparison.OrdinalIgnoreCase);
        if (!sameStart || !samePath)
        {
            throw new InvalidOperationException(
                $"Recorded Development PID {expected.ProcessId} no longer matches its creation time "
                + $"and executable path. Expected '{expectedPath}', found '{actualPath}'. "
                + "No process was terminated.");
        }
    }
}

public sealed record DevelopmentSessionRecord(
    int SchemaVersion,
    string SessionId,
    int ManagerProcessId,
    DateTimeOffset ManagerStartedAtUtc,
    string ManagerExecutablePath,
    IReadOnlyList<DevelopmentChildProcessRecord> Children)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record DevelopmentChildProcessRecord(
    string ProcessKey,
    int ProcessId,
    DateTimeOffset StartedAtUtc,
    string ExecutablePath,
    int StartOrder);
