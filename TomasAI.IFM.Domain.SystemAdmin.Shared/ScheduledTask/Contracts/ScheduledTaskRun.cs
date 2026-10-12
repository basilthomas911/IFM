using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;

/// <summary>Contains one admitted occurrence and its confirmed business outcome.</summary>
[MessagePackObject]
public sealed record ScheduledTaskRun
{
    [Key(0)] public ScheduledTaskId Id { get; init; }
    [Key(1)] public ScheduledTaskId ScheduleId { get; init; }
    [Key(2)] public long Revision { get; init; }
    [Key(3)] public long DefinitionRevision { get; init; }
    [Key(4)] public string TaskKey { get; init; } = "";
    [Key(5)] public string Environment { get; init; } = "";
    [Key(6)] public string HostId { get; init; } = "";
    [Key(7)] public DateTimeOffset IntendedFireTimeUtc { get; init; }
    [Key(8)] public bool Manual { get; init; }
    [Key(9)] public ScheduledTaskRunStatus Status { get; init; }
    [Key(10)] public DateTimeOffset? StartedAtUtc { get; init; }
    [Key(11)] public DateTimeOffset? FinishedAtUtc { get; init; }
    [Key(12)] public DateOnly? ValueDate { get; init; }
    [Key(13)] public string Stage { get; init; } = "";
    [Key(14)] public string Detail { get; init; } = "";
    [Key(15)] public int? ExitCode { get; init; }
    [Key(16)] public Guid OperationCommandId { get; init; }
    [Key(17)] public int? ProcessId { get; init; }
    [Key(18)] public string Reason { get; init; } = "";
    /// <summary>Gets the date whose complete EOD command set succeeded; later maintenance failures cannot erase it.</summary>
    [Key(19)] public DateOnly? CompletedEndOfDayValueDate { get; init; }
    /// <summary>Gets a bounded diagnostic tail of the task output, independently of full host-retained artifacts.</summary>
    [Key(20)] public string StandardOutputTail { get; init; } = "";
    /// <summary>Gets a bounded diagnostic tail of the task error output.</summary>
    [Key(21)] public string StandardErrorTail { get; init; } = "";
    /// <summary>Gets the relative artifact directory under the owning host task-run root.</summary>
    [Key(22)] public string OutputDirectory { get; init; } = "";
}
