using System.Diagnostics;
using System.Text;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Configuration;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Scylla;

internal enum ScyllaManagerOperation
{
    Version,
    Status,
    Backup,
    Tasks,
    BackupList,
    BackupFiles,
    RestoreSchema,
    RestoreTables
}

internal sealed record ScyllaManagerInvocation(
    ScyllaManagerOperation Operation,
    IReadOnlyList<string> Arguments,
    TimeSpan Timeout);

internal sealed record ScyllaManagerProcessResult(
    string StandardOutput,
    string StandardError,
    TimeSpan Elapsed);

internal interface IScyllaManagerProcessRunner
{
    ValueTask<ScyllaManagerProcessResult> RunAsync(
        ScyllaManagerInvocation invocation,
        CancellationToken cancellationToken);
}

internal sealed class ScyllaManagerProcessRunner(ScyllaBackupOptions options) : IScyllaManagerProcessRunner
{
    const int MaximumCapturedCharacters = 64 * 1024;
    readonly ScyllaBackupOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    public async ValueTask<ScyllaManagerProcessResult> RunAsync(
        ScyllaManagerInvocation invocation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        var executable = ResolveExecutable();
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = _options.ToolDirectory
        };
        foreach (var inheritedName in startInfo.Environment.Keys
                     .Where(static name => name.StartsWith("SCYLLA_MANAGER_", StringComparison.OrdinalIgnoreCase)).ToArray())
            startInfo.Environment.Remove(inheritedName);
        startInfo.Environment["SCYLLA_MANAGER_API_URL"] = _options.ManagerApiUrl;
        if (!string.IsNullOrWhiteSpace(_options.ManagerApiCertificateFile))
        {
            startInfo.Environment["SCYLLA_MANAGER_API_CERT_FILE"] = _options.ManagerApiCertificateFile;
            startInfo.Environment["SCYLLA_MANAGER_API_KEY_FILE"] = _options.ManagerApiKeyFile;
        }
        foreach (var argument in invocation.Arguments) startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        if (!process.Start()) throw new ScyllaManagerOperationException(invocation.Operation, null);
        var outputTask = ReadBoundedAsync(process.StandardOutput, cancellationToken);
        var errorTask = ReadBoundedAsync(process.StandardError, cancellationToken);
        var started = Stopwatch.GetTimestamp();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(invocation.Timeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            cancellationToken.ThrowIfCancellationRequested();
            throw new ScyllaManagerOperationException(invocation.Operation, null, timedOut: true);
        }
        var output = await outputTask.ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);
        if (process.ExitCode != 0)
            throw new ScyllaManagerOperationException(invocation.Operation, process.ExitCode);
        return new ScyllaManagerProcessResult(output, error, Stopwatch.GetElapsedTime(started));
    }

    string ResolveExecutable()
    {
        var path = Path.GetFullPath(Path.Combine(
            _options.ToolDirectory, OperatingSystem.IsWindows() ? "sctool.exe" : "sctool"));
        if (!PostgreSqlBackupOptions.IsWithin(path, _options.ToolDirectory) || !File.Exists(path))
            throw new InvalidOperationException("The required Scylla Manager client tool is unavailable.");
        return path;
    }

    static async Task<string> ReadBoundedAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var result = new StringBuilder();
        var buffer = new char[2048];
        while (true)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            if (result.Length < MaximumCapturedCharacters)
                result.Append(buffer, 0, Math.Min(read, MaximumCapturedCharacters - result.Length));
        }
        return result.ToString();
    }

    static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) { }
    }
}

internal sealed class ScyllaManagerOperationException(
    ScyllaManagerOperation operation,
    int? exitCode,
    bool timedOut = false)
    : InvalidOperationException(timedOut
        ? $"The Scylla Manager {SafeName(operation)} operation exceeded its configured timeout."
        : $"The Scylla Manager {SafeName(operation)} operation failed{(exitCode is null ? "." : $" with exit code {exitCode}.")}")
{
    public int? ExitCode { get; } = exitCode;
    public bool TimedOut { get; } = timedOut;

    static string SafeName(ScyllaManagerOperation operation) => operation switch
    {
        ScyllaManagerOperation.Version => "version check",
        ScyllaManagerOperation.Status => "cluster health check",
        ScyllaManagerOperation.Backup => "backup capture",
        ScyllaManagerOperation.Tasks => "task status check",
        ScyllaManagerOperation.BackupList => "backup listing",
        ScyllaManagerOperation.BackupFiles => "backup manifest listing",
        ScyllaManagerOperation.RestoreSchema => "schema restore",
        ScyllaManagerOperation.RestoreTables => "table restore",
        _ => "native"
    };
}
