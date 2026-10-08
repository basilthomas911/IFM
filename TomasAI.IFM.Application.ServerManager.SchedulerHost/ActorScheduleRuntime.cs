using TomasAI.IFM.Application.ServerManager.Contracts;
using System.Security.Cryptography;
using System.Text;
using MessagePack;
using Quartz;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Application.ServerManager.SchedulerHost;
/// <summary>Applies committed actor definitions to the single lease-owned Quartz engine.</summary>
public sealed class ActorScheduleRuntime(SchedulerHostOptions options, SchedulerOwnershipLease ownership,
    ISchedulerFactory schedulerFactory, IScheduledTaskCommandApi commands, IScheduledTaskQueryApi queries,
    ILogger<ActorScheduleRuntime> logger)
{
    public const string ManagedData = "actorManaged";
    public const string DefinitionRevisionData = "definitionRevision";
    public const string FingerprintData = "definitionFingerprint";
    private readonly SemaphoreSlim _installation = new(1, 1);
    /// <summary>Reconciles one current persisted desired definition and reports an installation receipt.</summary>
    public async Task ApplyAsync(ScheduledTaskDefinition requested, CancellationToken cancellationToken)
    {
        if (requested.Schedule.Environment != options.Environment || requested.Schedule.HostId != options.HostId) throw new InvalidOperationException("Scheduled definition targets another host.");
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        await _installation.WaitAsync(cancellationToken);
        try
        {
            await ownership.EnsureOwnedAsync(cancellationToken);
            var current = await queries.GetScheduledTaskAsync(new() { EntityId = requested.Id, Environment = options.Environment, HostId = options.HostId }, cancellationToken);
            Require(current);
            var definition = current.Value!;
            if (definition.DesiredRevision != requested.DesiredRevision) return;
            var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
            var key = new JobKey(definition.Id.Format(), "ifm-schedules");
            var triggerKey = new TriggerKey(definition.Id.Format(), "ifm-schedules");
            var fingerprint = Fingerprint(definition);
            if (!definition.Enabled || definition.Removed)
            {
                await scheduler.DeleteJob(key, cancellationToken);
                if (await scheduler.CheckExists(key, cancellationToken)) throw new InvalidOperationException("Quartz retained a disabled job after deletion.");
            }
            else
            {
                var preview = ScheduledTaskTimingRules.Preview(definition.Schedule, DateTimeOffset.UtcNow);
                var expiredOneTime = definition.Schedule.Timing == ScheduledTaskTiming.OneTime &&
                    definition.Schedule.StartsAtUtc is { } fire && fire <= DateTimeOffset.UtcNow &&
                    definition.AppliedRevision == definition.DesiredRevision;
                if (!preview.Valid && !expiredOneTime) throw new InvalidOperationException(string.Join(";", preview.Errors));
                var existing = await scheduler.GetJobDetail(key, cancellationToken);
                if (existing?.JobDataMap.GetString(FingerprintData) != fingerprint)
                {
                    var job = JobBuilder.Create<ExternalProcessJob>().WithIdentity(key).StoreDurably()
                        .UsingJobData(ManagedData, "true").UsingJobData(FingerprintData, fingerprint)
                        .UsingJobData(DefinitionRevisionData, definition.DesiredRevision.ToString(System.Globalization.CultureInfo.InvariantCulture))
                        .UsingJobData(ScheduledTaskExecutionService.TaskKeyData, definition.Schedule.TaskKey)
                        .UsingJobData(ScheduledTaskExecutionService.ScheduleDefinitionIdData, definition.Id.Value.ToString("D"))
                        .UsingJobData(ScheduledTaskExecutionService.MaximumRuntimeSecondsData, definition.Schedule.MaximumRuntimeSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)).Build();
                    await scheduler.AddJob(job, true, true, cancellationToken);
                }
                if (!expiredOneTime)
                {
                    var desiredTrigger = BuildTrigger(definition, key, triggerKey);
                    var actualTrigger = await scheduler.GetTrigger(triggerKey, cancellationToken);
                    if (!TriggerMatches(actualTrigger, desiredTrigger))
                    {
                        if (actualTrigger is not null) await scheduler.RescheduleJob(triggerKey, desiredTrigger, cancellationToken);
                        else await scheduler.ScheduleJob(desiredTrigger, cancellationToken);
                    }
                    if (!TriggerMatches(await scheduler.GetTrigger(triggerKey, cancellationToken), desiredTrigger))
                        throw new InvalidOperationException("Quartz trigger readback does not match the desired configuration.");
                }
                if ((await scheduler.GetJobDetail(key, cancellationToken))?.JobDataMap.GetString(FingerprintData) != fingerprint)
                    throw new InvalidOperationException("Quartz job readback does not match the desired configuration.");
            }
            if (definition.InstallationStatus != ScheduledTaskInstallationStatus.Applied || definition.AppliedRevision != definition.DesiredRevision || definition.InstallationFingerprint != fingerprint)
            {
                var receipt = await commands.RecordScheduledTaskInstallationAsync(new() { CommandId = Guid.NewGuid(), EntityId = definition.Id,
                    OperationCommandId = definition.OperationCommandId, Operator = "QuartzHost", DesiredRevision = definition.DesiredRevision,
                    AppliedEnabled = definition.Enabled && !definition.Removed, Fingerprint = fingerprint, Detail = "Quartz desired definition applied." }, cancellationToken);
                Require(receipt);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            logger.LogError(exception, "{Component}.{Method} Installation failed for {ScheduleId} {DesiredRevision}", nameof(ActorScheduleRuntime), nameof(ApplyAsync), requested.Id.Format(), requested.DesiredRevision);
            using var receiptDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var receipt = await commands.RecordScheduledTaskInstallationFailureAsync(new() { CommandId = Guid.NewGuid(), EntityId = requested.Id,
                OperationCommandId = requested.OperationCommandId, Operator = "QuartzHost", DesiredRevision = requested.DesiredRevision, Detail = exception.Message }, receiptDeadline.Token);
            Require(receipt);
            throw;
        }
        finally { ScheduledTaskTelemetry.Applied(requested.Schedule.TaskKey, options.Environment, started); _installation.Release(); }
    }
    /// <summary>Triggers a committed manual occurrence without creating a second schedule or bypassing actor admission.</summary>
    public async Task TriggerManualAsync(ScheduledTaskRun run, CancellationToken cancellationToken)
    {
        if (!run.Manual || run.Environment != options.Environment || run.HostId != options.HostId || run.Status != ScheduledTaskRunStatus.Requested) throw new InvalidOperationException("Manual occurrence targets another host or is not requested.");
        await ownership.EnsureOwnedAsync(cancellationToken);
        var current = await queries.GetScheduledTaskAsync(new() { EntityId = run.ScheduleId, Environment = options.Environment, HostId = options.HostId }, cancellationToken);
        Require(current);
        var latest = await queries.GetScheduledTaskRunAsync(new() { EntityId = run.Id, ScheduleId = run.ScheduleId,
            HostId = run.HostId, Environment = run.Environment, IntendedFireTimeUtc = run.IntendedFireTimeUtc }, cancellationToken);
        Require(latest);
        if (latest.Value!.Status != ScheduledTaskRunStatus.Requested) return;
        if (current.Value!.DesiredRevision != run.DefinitionRevision || !current.Value.Enabled || current.Value.Removed)
        {
            Require(await commands.RecordScheduledTaskRunAdmissionAsync(new() { CommandId = Guid.NewGuid(), EntityId = run.Id,
                OperationCommandId = run.OperationCommandId, Accepted = false, Detail = "Manual occurrence is obsolete or its schedule is disabled.", Operator = "QuartzHost" }, cancellationToken));
            return;
        }
        await ApplyAsync(current.Value, cancellationToken);
        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        await scheduler.TriggerJob(new JobKey(run.ScheduleId.Format(), "ifm-schedules"), new JobDataMap
        {
            [ScheduledTaskExecutionService.OriginData] = ScheduledRunOrigin.Manual.ToString(),
            [ScheduledTaskExecutionService.RunIdData] = run.Id.Value.ToString("D"),
            ["intendedFireUtc"] = run.IntendedFireTimeUtc.ToString("O"),
            ["operationCommandId"] = run.OperationCommandId.ToString("D"),
            ["runAlreadyRequested"] = "true"
        }, cancellationToken);
    }
    /// <summary>Compares persisted Quartz timing to the desired trigger without resetting an unchanged fire schedule.</summary>
    public static bool TriggerMatches(ITrigger? actual, ITrigger desired) => actual is not null &&
        actual.Key.Equals(desired.Key) && actual.JobKey.Equals(desired.JobKey) && actual.EndTimeUtc == desired.EndTimeUtc &&
        actual.MisfireInstruction == desired.MisfireInstruction &&
        (actual, desired) switch
        {
            (ICronTrigger left, ICronTrigger right) => left.CronExpressionString == right.CronExpressionString &&
                left.TimeZone.Id == right.TimeZone.Id && (right.StartTimeUtc <= DateTimeOffset.UtcNow || left.StartTimeUtc == right.StartTimeUtc),
            (ISimpleTrigger left, ISimpleTrigger right) => left.StartTimeUtc == right.StartTimeUtc && left.RepeatCount == right.RepeatCount && left.RepeatInterval == right.RepeatInterval,
            _ => false
        };
    /// <summary>Builds a non-catch-up trigger with explicit zone and configured date bounds.</summary>
    public static ITrigger BuildTrigger(ScheduledTaskDefinition definition, JobKey jobKey, TriggerKey triggerKey)
    {
        var schedule = definition.Schedule;
        var builder = TriggerBuilder.Create().WithIdentity(triggerKey).ForJob(jobKey);
        if (schedule.StartsAtUtc is { } start) builder.StartAt(start);
        if (schedule.EndsAtUtc is { } end) builder.EndAt(end);
        return schedule.Timing switch
        {
            ScheduledTaskTiming.Cron => builder.WithSchedule(CronScheduleBuilder.CronSchedule(schedule.Expression).InTimeZone(ScheduledTaskTimingRules.ResolveTimeZone(schedule.TimeZoneId)).WithMisfireHandlingInstructionDoNothing()).Build(),
            ScheduledTaskTiming.OneTime when schedule.StartsAtUtc is { } fire => builder.StartAt(fire).WithSchedule(SimpleScheduleBuilder.Create().WithMisfireHandlingInstructionNextWithRemainingCount()).Build(),
            _ => throw new InvalidOperationException("Unsupported scheduled-task timing.")
        };
    }
    /// <summary>Hashes only desired configuration; receipt revisions do not reinstall triggers.</summary>
    public static string Fingerprint(ScheduledTaskDefinition definition)
    {
        var timing = MessagePackSerializer.Serialize(definition.Schedule);
        var identity = Encoding.UTF8.GetBytes($"{definition.Id.Format()}:{definition.DesiredRevision}:{definition.Enabled}:{definition.Removed}:{definition.ManifestVersion}:{definition.ReviewedArtifactDigest}:");
        return Convert.ToHexString(SHA256.HashData(identity.Concat(timing).ToArray()));
    }
    /// <summary>Requires actor source-state acceptance before runtime processing continues.</summary>
    internal static void Require<T>(ServiceResult<T> result) where T : class
    { if (!result.Success || result.Value is null) throw new InvalidOperationException($"ScheduledTask actor request rejected: {result.ErrorMessage}"); }
}
