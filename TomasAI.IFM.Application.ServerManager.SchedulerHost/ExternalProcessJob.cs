using Quartz;
using System.Text.Json;
using TomasAI.IFM.Application.ServerManager.Contracts;

namespace TomasAI.IFM.Application.ServerManager.SchedulerHost;

[DisallowConcurrentExecution]
public sealed class ExternalProcessJob(ScheduledTaskExecutionService executionService) : IJob
{
    public Task Execute(IJobExecutionContext context) => executionService.ExecuteAsync(context);
}

public sealed class ScheduledTaskExecutionService(
    SchedulerHostOptions options,
    TaskCatalogProvider catalog,
    SchedulerStore store,
    ScheduledProcessRunner runner,
    DependencyProbeService dependencies,
    ActiveRunRegistry activeRuns,
    IHostApplicationLifetime applicationLifetime,
    ILogger<ScheduledTaskExecutionService> logger)
{
    public const string TaskKeyData = "taskKey";
    public const string ScheduleDefinitionIdData = "scheduleDefinitionId";
    public const string RunIdData = "runId";
    public const string OccurrenceIdData = "occurrenceId";
    public const string AttemptIdData = "attemptId";
    public const string OriginData = "origin";
    public const string MaximumRuntimeSecondsData = "maximumRuntimeSeconds";

    public async Task ExecuteAsync(IJobExecutionContext context)
    {
        var taskKey = context.MergedJobDataMap.GetString(TaskKeyData)
            ?? throw new InvalidOperationException("Quartz job data does not contain taskKey.");
        var task = catalog.GetRequired(taskKey);
        var scheduleIdText = context.MergedJobDataMap.GetString(ScheduleDefinitionIdData);
        var scheduleId = Guid.TryParse(scheduleIdText, out var parsedScheduleId) ? parsedScheduleId : (Guid?)null;
        var runId = ReadGuid(context, RunIdData) ?? Guid.NewGuid();
        var identity = new ScheduledProcessIdentity(
            runId,
            ReadGuid(context, OccurrenceIdData) ?? Guid.NewGuid(),
            ReadGuid(context, AttemptIdData) ?? Guid.NewGuid(),
            Enum.TryParse<ScheduledRunOrigin>(context.MergedJobDataMap.GetString(OriginData), out var origin)
                ? origin
                : ScheduledRunOrigin.Scheduled,
            context.ScheduledFireTimeUtc ?? DateTimeOffset.UtcNow,
            $"IFM.TaskControl.{runId:N}");
        using var active = activeRuns.Register(
            identity.RunId,
            context.CancellationToken,
            applicationLifetime.ApplicationStopping);
        var runDirectory = Path.Combine(options.TaskRunRoot, task.TaskKey, identity.RunId.ToString("N"));
        var stdoutPath = Path.Combine(runDirectory, "stdout.log");
        var stderrPath = Path.Combine(runDirectory, "stderr.log");
        var newRun = new NewScheduledRun(
            identity.RunId,
            identity.OccurrenceId,
            identity.AttemptId,
            scheduleId,
            task.TaskKey,
            identity.Origin,
            context.FireInstanceId,
            identity.ScheduledFireUtc,
            stdoutPath,
            stderrPath);
        Directory.CreateDirectory(runDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(runDirectory, "run.json"),
            JsonSerializer.Serialize(new
            {
                identity.RunId,
                identity.OccurrenceId,
                identity.AttemptId,
                ScheduleDefinitionId = scheduleId,
                task.TaskKey,
                identity.Origin,
                identity.ScheduledFireUtc,
                task.ManifestVersion
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }),
            CancellationToken.None);
        if (!await store.TryCreateRunAsync(newRun, CancellationToken.None))
        {
            await store.RecordTerminalRunAsync(
                newRun,
                ScheduledRunState.SkippedOverlap,
                "Another run already owns this schedule definition.",
                CancellationToken.None);
            logger.LogWarning("Skipped overlapping execution for schedule {ScheduleDefinitionId}.", scheduleId);
            return;
        }

        if (!string.Equals(task.RequiredEnvironment, options.Environment, StringComparison.OrdinalIgnoreCase))
        {
            await store.TransitionRunAsync(
                identity.RunId,
                ScheduledRunState.BlockedDependency,
                $"Task requires environment '{task.RequiredEnvironment}', host is '{options.Environment}'.",
                null,
                null,
                null,
                CancellationToken.None);
            return;
        }

        if (!task.IsExecutableAvailable(options))
        {
            await store.TransitionRunAsync(
                identity.RunId,
                ScheduledRunState.BlockedDependency,
                "Approved executable is not deployed or does not match its configured SHA-256 hash.",
                null,
                null,
                null,
                CancellationToken.None);
            return;
        }

        var blockingDependency = await dependencies.FindBlockingDependencyAsync(task, active.Token);
        if (blockingDependency is not null)
        {
            await store.TransitionRunAsync(
                identity.RunId,
                ScheduledRunState.BlockedDependency,
                blockingDependency,
                null,
                null,
                null,
                CancellationToken.None);
            return;
        }

        await store.TransitionRunAsync(
            identity.RunId,
            ScheduledRunState.Starting,
            null,
            null,
            null,
            null,
            CancellationToken.None);
        var result = await runner.RunAsync(
            task,
            identity,
            stdoutPath,
            stderrPath,
            async (processId, processStartedAt, cancellationToken) => await store.TransitionRunAsync(
                identity.RunId,
                ScheduledRunState.Running,
                null,
                processId,
                processStartedAt,
                null,
                CancellationToken.None),
            active.Token,
            int.TryParse(context.MergedJobDataMap.GetString(MaximumRuntimeSecondsData), out var runtimeSeconds)
                ? runtimeSeconds
                : null);

        if (result.State is ScheduledRunState.Cancelled or ScheduledRunState.ForceTerminated)
        {
            await store.TransitionRunAsync(
                identity.RunId,
                ScheduledRunState.Cancelling,
                "Scheduler shutdown or cancellation requested process termination.",
                result.ProcessId,
                result.ProcessStartedAtUtc,
                null,
                CancellationToken.None);
        }

        await store.TransitionRunAsync(
            identity.RunId,
            result.State,
            result.Detail,
            result.ProcessId,
            result.ProcessStartedAtUtc,
            result.ExitCode,
            CancellationToken.None);
        await store.RecordOutputDispositionAsync(
            identity.RunId,
            result.StdoutTruncated,
            result.StderrTruncated,
            CancellationToken.None);
    }

    private static Guid? ReadGuid(IJobExecutionContext context, string key)
        => Guid.TryParse(context.MergedJobDataMap.GetString(key), out var value) ? value : null;
}
