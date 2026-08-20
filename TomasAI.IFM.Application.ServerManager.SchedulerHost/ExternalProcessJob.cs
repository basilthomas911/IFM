using Quartz;
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
    ILogger<ScheduledTaskExecutionService> logger)
{
    public const string TaskKeyData = "taskKey";
    public const string ScheduleDefinitionIdData = "scheduleDefinitionId";

    public async Task ExecuteAsync(IJobExecutionContext context)
    {
        var taskKey = context.MergedJobDataMap.GetString(TaskKeyData)
            ?? throw new InvalidOperationException("Quartz job data does not contain taskKey.");
        var task = catalog.GetRequired(taskKey);
        var scheduleIdText = context.MergedJobDataMap.GetString(ScheduleDefinitionIdData);
        var scheduleId = Guid.TryParse(scheduleIdText, out var parsedScheduleId) ? parsedScheduleId : (Guid?)null;
        var identity = new ScheduledProcessIdentity(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            ScheduledRunOrigin.Scheduled,
            context.ScheduledFireTimeUtc ?? DateTimeOffset.UtcNow);
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
        if (!await store.TryCreateRunAsync(newRun, context.CancellationToken))
        {
            await store.RecordTerminalRunAsync(
                newRun,
                ScheduledRunState.SkippedOverlap,
                "Another run already owns this schedule definition.",
                context.CancellationToken);
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
                context.CancellationToken);
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
                context.CancellationToken);
            return;
        }

        await store.TransitionRunAsync(
            identity.RunId,
            ScheduledRunState.Starting,
            null,
            null,
            null,
            null,
            context.CancellationToken);
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
                cancellationToken),
            context.CancellationToken);

        if (result.State == ScheduledRunState.ForceTerminated)
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
    }
}
