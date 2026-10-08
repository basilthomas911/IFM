using Quartz;
using TomasAI.IFM.Application.ServerManager.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;

namespace TomasAI.IFM.Application.ServerManager.SchedulerHost;

/// <summary>Resolves a scheduled or operator-requested occurrence from Quartz execution data.</summary>
public sealed record ScheduledTaskOccurrence(ScheduledTaskId ScheduleId, long DefinitionRevision, string TaskKey,
    DateTimeOffset IntendedFireTimeUtc, bool Manual, ScheduledTaskId RunId, Guid OperationCommandId, bool AlreadyRequested)
{
    /// <summary>Uses the cron fire instant when manual-only data is absent; never substitutes the current clock.</summary>
    public static ScheduledTaskOccurrence Create(JobDataMap data, DateTimeOffset? scheduledFireTimeUtc)
    {
        var scheduleId = new ScheduledTaskId(Guid.Parse(data.GetString(ScheduledTaskExecutionService.ScheduleDefinitionIdData)!));
        var revision = long.Parse(data.GetString(ActorScheduleRuntime.DefinitionRevisionData)!, System.Globalization.CultureInfo.InvariantCulture);
        var taskKey = data.GetString(ScheduledTaskExecutionService.TaskKeyData)!;
        var fire = Optional("intendedFireUtc") is { } intended ? DateTimeOffset.Parse(intended, System.Globalization.CultureInfo.InvariantCulture)
            : scheduledFireTimeUtc ?? throw new InvalidOperationException("Scheduled occurrence has no intended fire timestamp.");
        var manual = Optional(ScheduledTaskExecutionService.OriginData) == ScheduledRunOrigin.Manual.ToString();
        var runId = manual ? new ScheduledTaskId(Guid.Parse(data.GetString(ScheduledTaskExecutionService.RunIdData)!)) : ScheduledTaskIdentities.Occurrence(scheduleId, fire);
        var operation = Optional("operationCommandId") is { } id ? Guid.Parse(id) : Guid.NewGuid();
        return new(scheduleId, revision, taskKey, fire, manual, runId, operation, manual && Optional("runAlreadyRequested") == "true");
        string? Optional(string key) => data.ContainsKey(key) ? data.GetString(key) : null;
    }
}
