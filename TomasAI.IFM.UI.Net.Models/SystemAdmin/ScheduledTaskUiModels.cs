namespace TomasAI.IFM.UI.Net.Models.SystemAdmin;
/// <summary>Contains editable timing and target settings owned by the scheduled-task UI.</summary>
public sealed record ScheduledTaskScheduleUiModel(string TaskKey, string Name, string Environment, string HostId,
    bool OneTime, string Expression, string TimeZoneId, DateTimeOffset? StartsAtUtc, DateTimeOffset? EndsAtUtc,
    int MaximumRuntimeSeconds, int DispatchToleranceSeconds, string Description);
/// <summary>Contains desired and confirmed installation state for a task list row.</summary>
public sealed record ScheduledTaskDefinitionUiModel(Guid Id, long Revision, long DesiredRevision, ScheduledTaskScheduleUiModel Schedule,
    bool Enabled, bool Removed, string InstallationStatus, long AppliedRevision, string InstallationDetail, Guid? ActiveRunId);
/// <summary>Contains reviewed deployed project availability for the task picker.</summary>
public sealed record ScheduledTaskProjectUiModel(string TaskKey, string Name, bool Available, string Platform, string ManifestVersion);
/// <summary>Contains one persisted occurrence and its business outcome.</summary>
public sealed record ScheduledTaskRunUiModel(Guid Id, DateTimeOffset IntendedFireTimeUtc, string Status, string Stage, string Detail,
    DateTimeOffset? StartedAtUtc, DateTimeOffset? FinishedAtUtc, int? ExitCode, DateOnly? ValueDate, long Revision = 0, Guid OperationCommandId = default, int? ProcessId = null, string StandardOutputTail = "", string StandardErrorTail = "", string OutputDirectory = "");
/// <summary>Contains bounded task list, catalog, host and selected task run state.</summary>
public sealed record ScheduledTaskDashboardUiModel(ScheduledTaskDefinitionUiModel[] Schedules, ScheduledTaskProjectUiModel[] Projects,
    ScheduledTaskRunUiModel[] Runs, string HostStatus, DateTimeOffset? HostObservedAtUtc);
/// <summary>Contains validated next UTC occurrences and useful timing errors.</summary>
public sealed record ScheduledTaskPreviewUiModel(bool Valid, string[] Errors, DateTimeOffset[] NextFireTimesUtc);

/// <summary>Contains a resumable occurrence-history page.</summary>
public sealed record ScheduledTaskRunPageUiModel(ScheduledTaskRunUiModel[] Runs, byte[]? PagingState);
/// <summary>Contains retained stdout text and its byte continuation.</summary>
public sealed record ScheduledTaskOutputPageUiModel(string Text, long NextOffset, bool EndOfOutput, bool Available);
