using System.Diagnostics;
using System.Text;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Configuration;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.PostgreSql;

internal enum PostgreSqlNativeTool
{
    BaseBackup,
    VerifyBackup,
    Control
}

internal sealed record PostgreSqlNativeInvocation(
    PostgreSqlNativeTool Tool,
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string?> Environment,
    TimeSpan Timeout);

internal sealed record PostgreSqlNativeResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    TimeSpan Elapsed);

internal interface IPostgreSqlNativeProcessRunner
{
    ValueTask<PostgreSqlNativeResult> RunAsync(
        PostgreSqlNativeInvocation invocation,
        CancellationToken cancellationToken);
}

internal sealed class PostgreSqlNativeProcessRunner(PostgreSqlBackupOptions options)
    : IPostgreSqlNativeProcessRunner
{
    const int MaximumCapturedCharacters = 32 * 1024;
    readonly PostgreSqlBackupOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    public async ValueTask<PostgreSqlNativeResult> RunAsync(
        PostgreSqlNativeInvocation invocation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        var executable = ResolveExecutable(invocation.Tool);
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
            .Where(static name => name.StartsWith("PG", StringComparison.OrdinalIgnoreCase)).ToArray())
            startInfo.Environment.Remove(inheritedName);
        foreach (var argument in invocation.Arguments) startInfo.ArgumentList.Add(argument);
        foreach (var (name, value) in invocation.Environment)
        {
            if (value is null) startInfo.Environment.Remove(name);
            else startInfo.Environment[name] = value;
        }

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        if (!process.Start()) throw new PostgreSqlNativeOperationException(invocation.Tool, null);
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
            throw new PostgreSqlNativeOperationException(invocation.Tool, null, timedOut: true);
        }
        var output = await outputTask.ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);
        var result = new PostgreSqlNativeResult(
            process.ExitCode,
            output,
            error,
            Stopwatch.GetElapsedTime(started));
        if (result.ExitCode != 0)
            throw new PostgreSqlNativeOperationException(invocation.Tool, result.ExitCode);
        return result;
    }

    string ResolveExecutable(PostgreSqlNativeTool tool)
    {
        var fileName = tool switch
        {
            PostgreSqlNativeTool.BaseBackup => OperatingSystem.IsWindows() ? "pg_basebackup.exe" : "pg_basebackup",
            PostgreSqlNativeTool.VerifyBackup => OperatingSystem.IsWindows() ? "pg_verifybackup.exe" : "pg_verifybackup",
            PostgreSqlNativeTool.Control => OperatingSystem.IsWindows() ? "pg_ctl.exe" : "pg_ctl",
            _ => throw new ArgumentOutOfRangeException(nameof(tool))
        };
        var path = Path.GetFullPath(Path.Combine(_options.ToolDirectory, fileName));
        if (!PostgreSqlBackupOptions.IsWithin(path, _options.ToolDirectory) || !File.Exists(path))
            throw new PostgreSqlNativeToolUnavailableException(tool);
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

internal sealed class PostgreSqlNativeToolUnavailableException(PostgreSqlNativeTool tool)
    : InvalidOperationException($"The required PostgreSQL {SafeName(tool)} tool is unavailable.")
{
    static string SafeName(PostgreSqlNativeTool value) => value switch
    {
        PostgreSqlNativeTool.BaseBackup => "base-backup",
        PostgreSqlNativeTool.VerifyBackup => "verification",
        PostgreSqlNativeTool.Control => "control",
        _ => "native"
    };
}

internal sealed class PostgreSqlNativeOperationException(
    PostgreSqlNativeTool tool,
    int? exitCode,
    bool timedOut = false)
    : InvalidOperationException(timedOut
        ? $"The PostgreSQL {SafeName(tool)} operation exceeded its configured timeout."
        : $"The PostgreSQL {SafeName(tool)} operation failed{(exitCode is null ? "." : $" with exit code {exitCode}.")}")
{
    public int? ExitCode { get; } = exitCode;
    public bool TimedOut { get; } = timedOut;

    static string SafeName(PostgreSqlNativeTool value) => value switch
    {
        PostgreSqlNativeTool.BaseBackup => "base-backup",
        PostgreSqlNativeTool.VerifyBackup => "verification",
        PostgreSqlNativeTool.Control => "control",
        _ => "native"
    };
}
