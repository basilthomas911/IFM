using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;

/// <summary>Identifies one actor-owned task schedule or its catalog/run aggregate route.</summary>
[MessagePackObject]
public readonly record struct ScheduledTaskId([property: Key(0)] Guid Value) : IActorEntityId
{
    [IgnoreMember] public bool IsValid => Value != Guid.Empty;
    /// <summary>Formats the stable actor route identity.</summary>
    public string Format() => Value.ToString("N");
}

public enum ScheduledTaskTiming : byte { Cron = 0, OneTime = 1 }
public enum ScheduledTaskInstallationStatus : byte { Pending = 0, Applied = 1, Failed = 2 }
public enum ScheduledTaskRunStatus : byte { Requested = 0, Admitted = 1, Running = 2, Succeeded = 3, Failed = 4, Uncertain = 5, Rejected = 6 }

/// <summary>Contains operator timing and target settings, independent of Quartz runtime objects.</summary>
[MessagePackObject]
public sealed record ScheduledTaskSchedule
{
    [Key(0)] public string TaskKey { get; init; } = "";
    [Key(1)] public string Name { get; init; } = "";
    [Key(2)] public string Environment { get; init; } = "Development";
    [Key(3)] public string HostId { get; init; } = "development";
    [Key(4)] public ScheduledTaskTiming Timing { get; init; }
    [Key(5)] public string Expression { get; init; } = "";
    [Key(6)] public string TimeZoneId { get; init; } = "America/New_York";
    [Key(7)] public DateTimeOffset? StartsAtUtc { get; init; }
    [Key(8)] public DateTimeOffset? EndsAtUtc { get; init; }
    [Key(9)] public int MaximumRuntimeSeconds { get; init; } = 1800;
    [Key(10)] public int DispatchToleranceSeconds { get; init; } = 60;
    [Key(11)] public string Description { get; init; } = "";
}

/// <summary>Contains the desired definition and independently confirmed runtime installation.</summary>
[MessagePackObject]
public sealed record ScheduledTaskDefinition
{
    [Key(0)] public ScheduledTaskId Id { get; init; }
    [Key(1)] public long Revision { get; init; }
    [Key(2)] public long DesiredRevision { get; init; }
    [Key(3)] public ScheduledTaskSchedule Schedule { get; init; } = new();
    [Key(4)] public bool Enabled { get; init; }
    [Key(5)] public bool Removed { get; init; }
    [Key(6)] public ScheduledTaskInstallationStatus InstallationStatus { get; init; }
    [Key(7)] public long AppliedRevision { get; init; }
    [Key(8)] public bool AppliedEnabled { get; init; }
    [Key(9)] public string InstallationDetail { get; init; } = "";
    [Key(10)] public string ManifestVersion { get; init; } = "";
    [Key(11)] public string UpdatedBy { get; init; } = "";
    [Key(12)] public DateTimeOffset UpdatedAtUtc { get; init; }
    [Key(13)] public Guid OperationCommandId { get; init; }
    [Key(14)] public Guid? ActiveRunId { get; init; }
    [Key(15)] public DateTimeOffset? LastAdmittedFireUtc { get; init; }
    [Key(16)] public string InstallationFingerprint { get; init; } = "";
    [Key(17)] public string ReviewedArtifactDigest { get; init; } = "";
}

/// <summary>Describes reviewed deployed code and bounded task execution capabilities.</summary>
[MessagePackObject]
public sealed record ScheduledTaskProject
{
    [Key(0)] public string TaskKey { get; init; } = "";
    [Key(1)] public string DisplayName { get; init; } = "";
    [Key(2)] public string ProjectName { get; init; } = "";
    [Key(3)] public string ManifestVersion { get; init; } = "";
    [Key(4)] public string Platform { get; init; } = "";
    [Key(5)] public string ArtifactDigest { get; init; } = "";
    [Key(6)] public bool Available { get; init; }
    [Key(7)] public int MaximumRuntimeSeconds { get; init; } = 1800;
    [Key(8)] public bool MarketSensitive { get; init; }
    [Key(9)] public string CompletionProtocol { get; init; } = "BusinessCompletion";
}

/// <summary>Contains persisted host identity/capability observations without live runtime reads.</summary>
[MessagePackObject]
public sealed record ScheduledTaskHostCapability
{
    [Key(0)] public string HostId { get; init; } = "";
    [Key(1)] public string Environment { get; init; } = "";
    [Key(2)] public string Platform { get; init; } = "";
    [Key(3)] public bool Ready { get; init; }
    [Key(4)] public DateTimeOffset ObservedAtUtc { get; init; }
    [Key(5)] public string Detail { get; init; } = "";
    [Key(6)] public long Generation { get; init; }
}

/// <summary>Contains actor-owned deployment facts for a bounded host catalog.</summary>
[MessagePackObject]
public sealed record ScheduledTaskCatalog
{
    [Key(0)] public ScheduledTaskId Id { get; init; }
    [Key(1)] public long Revision { get; init; }
    [Key(2)] public string HostId { get; init; } = "";
    [Key(3)] public string Environment { get; init; } = "";
    [Key(4)] public ScheduledTaskProject[] Projects { get; init; } = [];
    [Key(5)] public ScheduledTaskHostCapability? HostCapability { get; init; }
}

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

/// <summary>Contains projected schedules, catalog and a bounded recent-run page for the UI.</summary>
[MessagePackObject]
public sealed record ScheduledTasksDashboard
{
    [Key(0)] public ScheduledTaskCatalog? Catalog { get; init; }
    [Key(1)] public ScheduledTaskDefinition[] Schedules { get; init; } = [];
    [Key(2)] public ScheduledTaskRun[] RecentRuns { get; init; } = [];
    [Key(3)] public DateTimeOffset ReadAtUtc { get; init; }
}

/// <summary>Contains Quartz-validated next occurrences and useful validation failures.</summary>
[MessagePackObject]
public sealed record ScheduledTaskSchedulePreview
{
    [Key(0)] public bool Valid { get; init; }
    [Key(1)] public string[] Errors { get; init; } = [];
    [Key(2)] public DateTimeOffset[] NextFireTimesUtc { get; init; } = [];
    [Key(3)] public string TimeZoneId { get; init; } = "";
}
