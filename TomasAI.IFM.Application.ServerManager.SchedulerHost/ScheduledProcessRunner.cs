using System.Diagnostics;
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
        CancellationToken cancellationToken)
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

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(task.MaximumRuntimeSeconds));
            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            ScheduledRunState? forcedState = null;
            string? detail = null;
            try
            {
                await process.WaitForExitAsync(lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                forcedState = timeout.IsCancellationRequested
                    ? ScheduledRunState.TimedOut
                    : ScheduledRunState.ForceTerminated;
                detail = timeout.IsCancellationRequested
                    ? $"Maximum runtime of {task.MaximumRuntimeSeconds} seconds was exceeded."
                    : "Scheduler shutdown or cancellation required forced termination.";
                await RequestGracefulStopAsync(process, task);
                if (!process.HasExited)
                {
                    jobObject.Terminate();
                }

                await process.WaitForExitAsync(CancellationToken.None);
            }

            await Task.WhenAll(stdoutPump, stderrPump);
            if (forcedState is not null)
            {
                return new ScheduledProcessResult(forcedState.Value, process.Id, processStartedAt, process.ExitCode, detail);
            }

            var succeeded = task.SuccessExitCodes.Contains(process.ExitCode);
            return new ScheduledProcessResult(
                succeeded ? ScheduledRunState.Succeeded : ScheduledRunState.Failed,
                process.Id,
                processStartedAt,
                process.ExitCode,
                succeeded ? null : $"Process exited with unapproved code {process.ExitCode}.");
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

    private static async Task PumpAsync(StreamReader reader, StreamWriter writer)
    {
        while (await reader.ReadLineAsync() is { } line)
        {
            await writer.WriteLineAsync($"{DateTimeOffset.UtcNow:O} {line}");
        }
    }

    private static async Task RequestGracefulStopAsync(
        Process process,
        ScheduledTaskCatalogDefinition task)
    {
        try
        {
            var requested = task.GracefulStopMode switch
            {
                ScheduledTaskStopMode.CloseMainWindow => process.CloseMainWindow(),
                ScheduledTaskStopMode.StandardInput => WriteShutdownInput(process, task.ShutdownInput!),
                _ => false
            };
            if (!requested)
            {
                return;
            }

            using var grace = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await process.WaitForExitAsync(grace.Token);
        }
        catch (OperationCanceledException)
        {
            // The caller performs the required Job Object fallback.
        }
        catch (InvalidOperationException)
        {
            // Process already exited.
        }
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
    DateTimeOffset ScheduledFireUtc);

public sealed record ScheduledProcessResult(
    ScheduledRunState State,
    int? ProcessId,
    DateTimeOffset? ProcessStartedAtUtc,
    int? ExitCode,
    string? Detail);
