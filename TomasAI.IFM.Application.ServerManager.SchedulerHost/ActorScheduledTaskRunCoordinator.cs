using Quartz;
using TomasAI.IFM.Application.ServerManager.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.ServiceApi;
namespace TomasAI.IFM.Application.ServerManager.SchedulerHost;
/// <summary>Launches actor-managed occurrences only after durable run and schedule admission.</summary>
public sealed class ActorScheduledTaskRunCoordinator(SchedulerHostOptions options, SchedulerOwnershipLease ownership,
    TaskCatalogProvider catalog, ScheduledProcessRunner runner, DependencyProbeService dependencies,
    ActiveRunRegistry activeRuns, IHostApplicationLifetime lifetime, IScheduledTaskCommandApi commands,
    ILogger<ActorScheduledTaskRunCoordinator> logger)
{
    /// <summary>Executes one stable occurrence and records process and business outcome receipts.</summary>
    public async Task ExecuteAsync(IJobExecutionContext context)
    {
        await ownership.EnsureOwnedAsync(context.CancellationToken);
        var data = context.MergedJobDataMap;
        var occurrence = ScheduledTaskOccurrence.Create(data, context.ScheduledFireTimeUtc);
        var scheduleId = occurrence.ScheduleId;
        var revision = occurrence.DefinitionRevision;
        var task = catalog.GetRequired(occurrence.TaskKey);
        var fire = occurrence.IntendedFireTimeUtc;
        var manual = occurrence.Manual;
        var runId = occurrence.RunId;
        using var active = activeRuns.Register(runId.Value, context.CancellationToken, lifetime.ApplicationStopping);
        using var admissionDeadline = CancellationTokenSource.CreateLinkedTokenSource(active.Token);
        admissionDeadline.CancelAfter(TimeSpan.FromSeconds(15));
        var operationCommandId = occurrence.OperationCommandId;
        if (!occurrence.AlreadyRequested)
        {
            var request = await commands.RequestScheduledTaskRunAsync(new() { CommandId = operationCommandId, EntityId = runId, ScheduleId = scheduleId,
                DefinitionRevision = revision, IntendedFireTimeUtc = fire, Manual = manual, TaskKey = task.TaskKey,
                HostId = options.HostId, Environment = options.Environment, Operator = "QuartzHost" }, admissionDeadline.Token);
            if (!request.Success)
            {
                logger.LogWarning("{Component}.{Method} Occurrence not launched: {RunId} {Reason}", nameof(ActorScheduledTaskRunCoordinator), nameof(ExecuteAsync), runId.Format(), request.ErrorMessage);
                return;
            }
        }
        var admission = await commands.AdmitScheduledTaskRunAsync(new() { CommandId = Guid.NewGuid(), EntityId = scheduleId,
            RunId = runId.Value, DefinitionRevision = revision, IntendedFireTimeUtc = fire, Manual = manual,
            HostId = options.HostId, Environment = options.Environment, OperationCommandId = operationCommandId, Operator = "QuartzHost" }, admissionDeadline.Token);
        ActorScheduleRuntime.Require(await commands.RecordScheduledTaskRunAdmissionAsync(new() { CommandId = Guid.NewGuid(), OperationCommandId = operationCommandId, EntityId = runId,
            Accepted = admission.Success, Detail = admission.Success ? "Owning schedule admitted this occurrence." : admission.ErrorMessage, Operator = "QuartzHost" }, admissionDeadline.Token));
        if (!admission.Success) return;
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var processStarted = false;
        var terminalRecorded = false;
        try
        {
            if (task.RequiredEnvironment != options.Environment || !task.IsExecutableAvailable(options)) throw new InvalidOperationException("Approved task executable is unavailable or belongs to another environment.");
            var blockingDependency = await dependencies.FindBlockingDependencyAsync(task, admissionDeadline.Token);
            if (blockingDependency is not null) throw new InvalidOperationException(blockingDependency);
            await ownership.EnsureOwnedAsync(admissionDeadline.Token);
            var directory = Path.Combine(options.TaskRunRoot, task.TaskKey, runId.Format());
            Directory.CreateDirectory(directory);
            var identity = new ScheduledProcessIdentity(runId.Value, runId.Value, Guid.NewGuid(), manual ? ScheduledRunOrigin.Manual : ScheduledRunOrigin.Scheduled, fire, $"IFM.TaskControl.{runId.Format()}", scheduleId.Value, options.HostId, operationCommandId);
            var result = await runner.RunAsync(task, identity, Path.Combine(directory, "stdout.log"), Path.Combine(directory, "stderr.log"),
                async (pid, started, cancellationToken) =>
                {
                    processStarted = true;
                    ActorScheduleRuntime.Require(await commands.RecordScheduledTaskRunStartedAsync(new() { CommandId = Guid.NewGuid(), OperationCommandId = operationCommandId, EntityId = runId,
                        ProcessId = pid, StartedAtUtc = started, Operator = "QuartzHost" }, cancellationToken));
                }, active.Token, int.Parse(data.GetString(ScheduledTaskExecutionService.MaximumRuntimeSecondsData)!, System.Globalization.CultureInfo.InvariantCulture));
            var stdout = await ReadOutputTailAsync(Path.Combine(directory, "stdout.log"));
            var stderr = await ReadOutputTailAsync(Path.Combine(directory, "stderr.log"));
            var artifact = task.TaskKey + "/" + runId.Format();
            using var receiptDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            if (result.State == ScheduledRunState.Succeeded && result.ExitCode == 0)
                ActorScheduleRuntime.Require(await commands.CompleteScheduledTaskRunAsync(new() { CommandId = Guid.NewGuid(), OperationCommandId = operationCommandId, EntityId = runId, FinishedAtUtc = DateTimeOffset.UtcNow,
                    StandardOutputTail = stdout, StandardErrorTail = stderr, OutputDirectory = artifact, ExitCode = 0, Stage = "BusinessCompleted", Detail = result.Detail ?? "Task confirmed business completion by successful exit.", Operator = "QuartzHost" }, receiptDeadline.Token));
            else if (result.State is ScheduledRunState.TimedOut or ScheduledRunState.Cancelled or ScheduledRunState.ForceTerminated || result.ExitCode is null)
            {
                ActorScheduleRuntime.Require(await commands.RecordScheduledTaskRunUncertainAsync(new() { CommandId = Guid.NewGuid(), OperationCommandId = operationCommandId, EntityId = runId, FinishedAtUtc = DateTimeOffset.UtcNow,
                    StandardOutputTail = stdout, StandardErrorTail = stderr, OutputDirectory = artifact, Detail = result.Detail ?? "Process terminated without a confirmed business outcome.", Operator = "QuartzHost" }, receiptDeadline.Token));
                return; // Keep the schedule reservation until the uncertain business outcome is explicitly resolved.
            }
            else
                ActorScheduleRuntime.Require(await commands.FailScheduledTaskRunAsync(new() { CommandId = Guid.NewGuid(), OperationCommandId = operationCommandId, EntityId = runId, FinishedAtUtc = DateTimeOffset.UtcNow,
                    StandardOutputTail = stdout, StandardErrorTail = stderr, OutputDirectory = artifact, ExitCode = result.ExitCode, Stage = "ProcessTerminated", Detail = result.Detail ?? result.State.ToString(), Operator = "QuartzHost" }, receiptDeadline.Token));
            terminalRecorded = true;
            ActorScheduleRuntime.Require(await commands.RecordScheduledTaskRunCompletionAsync(new() { CommandId = Guid.NewGuid(), OperationCommandId = operationCommandId, EntityId = scheduleId, RunId = runId.Value, Operator = "QuartzHost" }, receiptDeadline.Token));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "{Component}.{Method} Run receipt failed for {ScheduleId} {RunId} {ProcessStarted}", nameof(ActorScheduledTaskRunCoordinator), nameof(ExecuteAsync), scheduleId.Format(), runId.Format(), processStarted);
            using var receiptDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            if (terminalRecorded) return; // Reservation reconciliation completes release without changing a committed outcome.
            if (processStarted)
                ActorScheduleRuntime.Require(await commands.RecordScheduledTaskRunUncertainAsync(new() { CommandId = Guid.NewGuid(), OperationCommandId = operationCommandId, EntityId = runId, FinishedAtUtc = DateTimeOffset.UtcNow, Detail = exception.Message, Operator = "QuartzHost" }, receiptDeadline.Token));
            else
            {
                ActorScheduleRuntime.Require(await commands.FailScheduledTaskRunAsync(new() { CommandId = Guid.NewGuid(), OperationCommandId = operationCommandId, EntityId = runId, FinishedAtUtc = DateTimeOffset.UtcNow, Stage = "BeforeLaunch", Detail = exception.Message, Operator = "QuartzHost" }, receiptDeadline.Token));
                ActorScheduleRuntime.Require(await commands.RecordScheduledTaskRunCompletionAsync(new() { CommandId = Guid.NewGuid(), OperationCommandId = operationCommandId, EntityId = scheduleId, RunId = runId.Value, Operator = "QuartzHost" }, receiptDeadline.Token));
            }
        }
        finally { ScheduledTaskTelemetry.Ran(task.TaskKey, options.Environment, started); }
    }
    /// <summary>Reads at most the last 16 KiB; missing log output cannot change a confirmed business result.</summary>
    private static async Task<string> ReadOutputTailAsync(string path)
    {
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, true);
            stream.Seek(Math.Max(0, stream.Length - 16384), SeekOrigin.Begin);
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return "Output artifact is unavailable; consult the host log."; }
    }

}
