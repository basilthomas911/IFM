using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using TomasAI.IFM.Application.ServerManager.Contracts;

namespace TomasAI.IFM.Application.ServerManager.SchedulerHost;

public sealed class ScheduledProcessRunner(
    SchedulerHostOptions hostOptions,
    ILogger<ScheduledProcessRunner> logger)
{
    public async Task<ScheduledProcessResult> RunAsync(
        ScheduledTaskCatalogDefinition task,
        ScheduledProcessIdentity identity,
        string stdoutPath,
        string stderrPath,
        Func<int, DateTimeOffset, CancellationToken, Task> onStarted,
        CancellationToken cancellationToken,
        int? maximumRuntimeSeconds = null)
    {
        var startInfo = CreateStartInfo(task, identity);
        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        using var jobObject = new WindowsJobObject();
        try
        {
            if (!process.Start())
            {
                return new ScheduledProcessResult(ScheduledRunState.Failed, null, null, null, "Process.Start returned false.");
            }

            try
            {
                jobObject.Assign(process);
            }
            catch
            {
                process.Kill(entireProcessTree: true);
                throw;
            }

            var processStartedAt = new DateTimeOffset(process.StartTime.ToUniversalTime());
            await onStarted(process.Id, processStartedAt, cancellationToken);

            await using var stdout = CreateWriter(stdoutPath);
            await using var stderr = CreateWriter(stderrPath);
            var stdoutPump = PumpAsync(process.StandardOutput, stdout);
            var stderrPump = PumpAsync(process.StandardError, stderr);

            var runtimeSeconds = maximumRuntimeSeconds ?? task.MaximumRuntimeSeconds;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(runtimeSeconds));
            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            ScheduledRunState? interruptedState = null;
            string? detail = null;
            try
            {
                await process.WaitForExitAsync(lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                var timedOut = timeout.IsCancellationRequested;
                detail = timeout.IsCancellationRequested
                    ? $"Maximum runtime of {runtimeSeconds} seconds was exceeded."
                    : "Scheduler cancellation was requested.";
                var stoppedCooperatively = await RequestGracefulStopAsync(process, task, identity.ControlPipeName);
                if (!process.HasExited)
                {
                    jobObject.Terminate();
                }

                await process.WaitForExitAsync(CancellationToken.None);
                interruptedState = timedOut
                    ? ScheduledRunState.TimedOut
                    : stoppedCooperatively
                        ? ScheduledRunState.Cancelled
                        : ScheduledRunState.ForceTerminated;
            }

            var output = await Task.WhenAll(stdoutPump, stderrPump);
            if (interruptedState is not null)
            {
                return new ScheduledProcessResult(
                    interruptedState.Value,
                    process.Id,
                    processStartedAt,
                    process.ExitCode,
                    detail,
                    output[0].Truncated,
                    output[1].Truncated);
            }

            var succeeded = task.SuccessExitCodes.Contains(process.ExitCode);
            return new ScheduledProcessResult(
                succeeded ? ScheduledRunState.Succeeded : ScheduledRunState.Failed,
                process.Id,
                processStartedAt,
                process.ExitCode,
                succeeded ? null : $"Process exited with unapproved code {process.ExitCode}.",
                output[0].Truncated,
                output[1].Truncated);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Scheduled task {TaskKey} failed during process execution.", task.TaskKey);
            return new ScheduledProcessResult(ScheduledRunState.Failed, null, null, null, exception.Message);
        }
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
            .Concat(["SystemRoot", "WINDIR", "TEMP", "TMP"])
            .Distinct(StringComparer.OrdinalIgnoreCase)
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
        startInfo.Environment["IFM_ENVIRONMENT"] = hostOptions.Environment;
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

    private async Task<OutputPumpResult> PumpAsync(StreamReader reader, StreamWriter writer)
    {
        long bytesWritten = 0;
        var truncated = false;
        while (await reader.ReadLineAsync() is { } line)
        {
            var bounded = line.Length <= hostOptions.MaximumOutputLineCharacters
                ? line
                : line[..hostOptions.MaximumOutputLineCharacters] + " [LINE TRUNCATED]";
            var rendered = $"{DateTimeOffset.UtcNow:O} {bounded}";
            var bytes = Encoding.UTF8.GetByteCount(rendered) + Environment.NewLine.Length;
            if (!truncated && bytesWritten + bytes <= hostOptions.MaximumOutputBytesPerStream)
            {
                await writer.WriteLineAsync(rendered);
                bytesWritten += bytes;
            }
            else if (!truncated)
            {
                truncated = true;
                await writer.WriteLineAsync($"{DateTimeOffset.UtcNow:O} [OUTPUT TRUNCATED: configured stream limit reached]");
            }
        }

        return new OutputPumpResult(bytesWritten, truncated);
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
    string ControlPipeName = "");

public sealed record ScheduledProcessResult(
    ScheduledRunState State,
    int? ProcessId,
    DateTimeOffset? ProcessStartedAtUtc,
    int? ExitCode,
    string? Detail,
    bool StdoutTruncated = false,
    bool StderrTruncated = false);

internal sealed record OutputPumpResult(long BytesWritten, bool Truncated);
