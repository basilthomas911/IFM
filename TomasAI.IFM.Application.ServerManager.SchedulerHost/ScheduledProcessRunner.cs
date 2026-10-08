using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using TomasAI.IFM.Application.ServerManager.Contracts;

namespace TomasAI.IFM.Application.ServerManager.SchedulerHost;

/// <summary>Runs a catalog task with bounded lifetime, descendant containment and output capture.</summary>
public sealed class ScheduledProcessRunner(
    SchedulerHostOptions hostOptions,
    ILogger<ScheduledProcessRunner> logger)
{
    /// <summary>Launches only the approved task and reports interruption without implying business rollback.</summary>
    public async Task<ScheduledProcessResult> RunAsync(
        ScheduledTaskCatalogDefinition task,
        ScheduledProcessIdentity identity,
        string stdoutPath,
        string stderrPath,
        Func<int, DateTimeOffset, CancellationToken, Task> onStarted,
        CancellationToken cancellationToken,
        int? maximumRuntimeSeconds = null)
    {
        var runtimeSeconds = maximumRuntimeSeconds ?? task.MaximumRuntimeSeconds;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(runtimeSeconds);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(runtimeSeconds));
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        using var outputLifetime = new CancellationTokenSource();
        using var process = new Process { StartInfo = CreateStartInfo(task, identity), EnableRaisingEvents = true };
        IScheduledProcessContainment? containment = null;
        StreamWriter? stdout = null;
        StreamWriter? stderr = null;
        Task<OutputPumpResult>? stdoutPump = null;
        Task<OutputPumpResult>? stderrPump = null;
        int? pid = null;
        DateTimeOffset? startedAt = null;
        var state = ScheduledRunState.Failed;
        string? detail = null;
        int? exitCode = null;
        var output = new[] { new OutputPumpResult(0, false), new OutputPumpResult(0, false) };
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            containment = ScheduledProcessContainment.Create();
            containment.Prepare(process.StartInfo);
            stdout = CreateWriter(stdoutPath);
            stderr = CreateWriter(stderrPath);
            if (!process.Start())
                throw new InvalidOperationException("SchedulerHost.PROCESS.START_FAILED; Process.Start returned false.");
            pid = process.Id;
            startedAt = new DateTimeOffset(process.StartTime.ToUniversalTime());
            containment.Assign(process);
            stdoutPump = PumpAsync(process.StandardOutput, stdout, outputLifetime.Token);
            stderrPump = PumpAsync(process.StandardError, stderr, outputLifetime.Token);
            // A failed log sink must not leave a child blocked forever on its redirected pipe.
            _ = stdoutPump.ContinueWith(_ => CancelLifetime(lifetime), CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            _ = stderrPump.ContinueWith(_ => CancelLifetime(lifetime), CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            await onStarted(pid.Value, startedAt.Value, lifetime.Token)
                .WaitAsync(TimeSpan.FromSeconds(hostOptions.ProcessStartTimeoutSeconds), lifetime.Token);
            await process.WaitForExitAsync(lifetime.Token);
            exitCode = process.ExitCode;
            state = task.SuccessExitCodes.Contains(exitCode.Value) ? ScheduledRunState.Succeeded : ScheduledRunState.Failed;
            detail = state == ScheduledRunState.Succeeded ? null : $"Process exited with unapproved code {exitCode}.";
        }
        catch (OperationCanceledException)
        {
            var cooperative = pid is not null
                && await RequestGracefulStopAsync(process, task, identity.ControlPipeName);
            state = timeout.IsCancellationRequested ? ScheduledRunState.TimedOut
                : cooperative ? ScheduledRunState.Cancelled : ScheduledRunState.ForceTerminated;
            detail = timeout.IsCancellationRequested ? $"Maximum runtime of {runtimeSeconds} seconds was exceeded."
                : "Scheduler cancellation was requested.";
        }
        catch (Exception exception)
        {
            detail = exception.Message;
            logger.LogError(exception,
                "Method {Method} task {TaskKey} run {RunId} runtime {RuntimeSeconds} failed during process execution.",
                nameof(RunAsync), task.TaskKey, identity.RunId, runtimeSeconds);
        }
        finally
        {
            try
            {
                // Always retire descendants, including after a successful parent exit or callback failure.
                containment?.Terminate();
                if (pid is not null)
                {
                    await process.WaitForExitAsync(CancellationToken.None)
                        .WaitAsync(TimeSpan.FromSeconds(hostOptions.ProcessTerminationTimeoutSeconds));
                    exitCode = process.ExitCode;
                }
            }
            catch (Exception exception)
            {
                state = ScheduledRunState.Failed;
                detail = $"SchedulerHost.PROCESS.EXIT_UNCERTAIN; {exception.Message}";
                logger.LogError(exception, "Method {Method} task {TaskKey} run {RunId} exit was not confirmed.",
                    nameof(RunAsync), task.TaskKey, identity.RunId);
            }

            if (stdoutPump is not null && stderrPump is not null)
            {
                try
                {
                    output = await Task.WhenAll(stdoutPump, stderrPump)
                        .WaitAsync(TimeSpan.FromSeconds(hostOptions.OutputDrainTimeoutSeconds));
                }
                catch (Exception exception)
                {
                    outputLifetime.Cancel();
                    state = ScheduledRunState.Failed;
                    detail = $"SchedulerHost.PROCESS.OUTPUT_INCOMPLETE; {exception.Message}";
                    _ = Task.WhenAll(stdoutPump, stderrPump).ContinueWith(t => _ = t.Exception,
                        CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
                }
            }
            outputLifetime.Cancel();
            stdout?.Dispose();
            stderr?.Dispose();
            try { containment?.Dispose(); }
            catch (Exception exception)
            {
                state = ScheduledRunState.Failed;
                detail = $"SchedulerHost.PROCESS.CONTAINMENT_UNCERTAIN; {exception.Message}";
                logger.LogError(exception, "Method {Method} run {RunId} containment cleanup failed.", nameof(RunAsync), identity.RunId);
            }
        }
        return new ScheduledProcessResult(state, pid, startedAt, exitCode, detail, output[0].Truncated, output[1].Truncated);
    }

    private ProcessStartInfo CreateStartInfo(
        ScheduledTaskCatalogDefinition task,
        ScheduledProcessIdentity identity)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = task.ResolveExecutablePath(hostOptions),
            WorkingDirectory = task.ResolveWorkingDirectory(hostOptions),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = task.GracefulStopMode == ScheduledTaskStopMode.StandardInput
        };
        foreach (var argument in task.DefaultArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        var inheritedNames = task.EnvironmentAllowlist
            .Concat(OperatingSystem.IsWindows() ? ["SystemRoot", "WINDIR", "TEMP", "TMP"] : new[] { "PATH", "TMPDIR", "LANG", "LC_ALL", "TZ" })
            .Distinct((OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal))
            .ToArray();
        var inheritedValues = inheritedNames
            .Select(name => (Name: name, Value: System.Environment.GetEnvironmentVariable(name)))
            .Where(item => item.Value is not null)
            .ToArray();
        startInfo.Environment.Clear();
        foreach (var item in inheritedValues)
        {
            startInfo.Environment[item.Name] = item.Value!;
        }

        startInfo.Environment["IFM_SCHEDULED_OCCURRENCE_ID"] = identity.OccurrenceId.ToString("D");
        startInfo.Environment["IFM_SCHEDULED_RUN_ID"] = identity.RunId.ToString("D");
        startInfo.Environment["IFM_SCHEDULED_ATTEMPT_ID"] = identity.AttemptId.ToString("D");
        startInfo.Environment["IFM_SCHEDULED_FIRE_UTC"] = identity.ScheduledFireUtc.ToString("O");
        startInfo.Environment["IFM_SCHEDULED_ORIGIN"] = identity.Origin.ToString();
        startInfo.Environment["IFM_SCHEDULED_DEFINITION_ID"] = identity.ScheduleId.ToString("D");
        startInfo.Environment["IFM_SCHEDULED_HOST_ID"] = identity.HostId;
        startInfo.Environment["IFM_SCHEDULED_CORRELATION_ID"] = identity.OperationCommandId.ToString("D");
        startInfo.Environment["IFM_ENVIRONMENT"] = hostOptions.Environment;
        startInfo.Environment["DOTNET_ENVIRONMENT"] = hostOptions.Environment;
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = hostOptions.Environment;
        startInfo.Environment["IFM_TASK_CONTROL_PIPE"] = identity.ControlPipeName;
        return startInfo;
    }

    private static StreamWriter CreateWriter(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return new StreamWriter(
            new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 64 * 1024, useAsync: true),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = true
        };
    }

    /// <summary>Reads in fixed buffers and retains at most the configured line and stream bounds.</summary>
    private async Task<OutputPumpResult> PumpAsync(StreamReader reader, StreamWriter writer, CancellationToken cancellationToken)
    {
        var buffer = new char[4096];
        var line = new StringBuilder();
        long bytesWritten = 0;
        var truncated = false;
        var lineTruncated = false;

        async Task FlushLineAsync()
        {
            var rendered = $"{DateTimeOffset.UtcNow:O} {line}" + (lineTruncated ? " [LINE TRUNCATED]" : "");
            var bytes = Encoding.UTF8.GetByteCount(rendered) + Environment.NewLine.Length;
            if (!truncated && bytesWritten + bytes <= hostOptions.MaximumOutputBytesPerStream)
            {
                await writer.WriteLineAsync(rendered.AsMemory(), cancellationToken);
                bytesWritten += bytes;
            }
            else if (!truncated)
            {
                truncated = true;
                await writer.WriteLineAsync($"{DateTimeOffset.UtcNow:O} [OUTPUT TRUNCATED: configured stream limit reached]".AsMemory(), cancellationToken);
            }
            line.Clear();
            lineTruncated = false;
        }

        while (true)
        {
            var count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (count == 0) break;
            for (var index = 0; index < count; index++)
            {
                var character = buffer[index];
                if (character == '\n') await FlushLineAsync();
                else if (character != '\r')
                {
                    if (line.Length < hostOptions.MaximumOutputLineCharacters) line.Append(character);
                    else lineTruncated = true;
                }
            }
        }
        if (line.Length > 0 || lineTruncated) await FlushLineAsync();
        return new OutputPumpResult(bytesWritten, truncated);
    }

    private static void CancelLifetime(CancellationTokenSource lifetime)
    {
        try { lifetime.Cancel(); }
        catch (ObjectDisposedException) { }
    }

    private static async Task<bool> RequestGracefulStopAsync(
        Process process,
        ScheduledTaskCatalogDefinition task,
        string controlPipeName)
    {
        try
        {
            var requested = task.GracefulStopMode switch
            {
                ScheduledTaskStopMode.CloseMainWindow => process.CloseMainWindow(),
                ScheduledTaskStopMode.StandardInput => WriteShutdownInput(process, task.ShutdownInput!),
                ScheduledTaskStopMode.NamedPipe => await WriteNamedPipeCancelAsync(controlPipeName),
                _ => false
            };
            if (!requested)
            {
                return false;
            }

            using var grace = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await process.WaitForExitAsync(grace.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            // The caller performs the required Job Object fallback.
            return false;
        }
        catch (InvalidOperationException)
        {
            // Process already exited.
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static async Task<bool> WriteNamedPipeCancelAsync(string controlPipeName)
    {
        await using var pipe = new NamedPipeClientStream(".", controlPipeName, PipeDirection.Out, PipeOptions.Asynchronous);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await pipe.ConnectAsync(timeout.Token);
        await using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
        await writer.WriteLineAsync("Cancel");
        return true;
    }

    private static bool WriteShutdownInput(Process process, string input)
    {
        process.StandardInput.WriteLine(input);
        process.StandardInput.Flush();
        return true;
    }
}

public sealed record ScheduledProcessIdentity(
    Guid RunId,
    Guid OccurrenceId,
    Guid AttemptId,
    ScheduledRunOrigin Origin,
    DateTimeOffset ScheduledFireUtc,
    string ControlPipeName = "",
    Guid ScheduleId = default,
    string HostId = "",
    Guid OperationCommandId = default);

public sealed record ScheduledProcessResult(
    ScheduledRunState State,
    int? ProcessId,
    DateTimeOffset? ProcessStartedAtUtc,
    int? ExitCode,
    string? Detail,
    bool StdoutTruncated = false,
    bool StderrTruncated = false);

internal sealed record OutputPumpResult(long BytesWritten, bool Truncated);
