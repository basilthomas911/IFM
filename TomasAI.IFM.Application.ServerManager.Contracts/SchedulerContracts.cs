namespace TomasAI.IFM.Application.ServerManager.Contracts;

public static class SchedulerProtocol
{
    public const int Version = 1;
    public const int MaximumFrameBytes = 1024 * 1024;
    public const string GetDashboardOperation = "scheduler.dashboard.get";
}

public sealed record SchedulerPipeRequest(
    int Version,
    Guid RequestId,
    string Operation,
    DateTimeOffset OriginatedAtUtc);

public sealed record SchedulerPipeResponse(
    int Version,
    Guid RequestId,
    bool Success,
    string? ErrorCode,
    string? ErrorMessage,
    SchedulerDashboardDto? Dashboard);

public sealed record SchedulerDashboardDto(
    SchedulerHealthDto Health,
    IReadOnlyList<TaskCatalogItemDto> TaskCatalog,
    IReadOnlyList<ScheduleSummaryDto> Schedules,
    IReadOnlyList<TaskRunSummaryDto> RecentRuns,
    DateTimeOffset GeneratedAtUtc);

public sealed record SchedulerHealthDto(
    SchedulerServiceState State,
    string Version,
    bool DatabaseAvailable,
    bool QuartzAvailable,
    bool SchedulingStarted,
    string Message,
    DateTimeOffset ObservedAtUtc);

public sealed record TaskCatalogItemDto(
    string TaskKey,
    string DisplayName,
    string Description,
    string ExecutablePath,
    string RequiredEnvironment,
    SchedulerRiskClassification RiskClassification,
    string ManifestVersion,
    bool ExecutableAvailable,
    int MaximumRuntimeSeconds);

public sealed record ScheduleSummaryDto(
    Guid ScheduleDefinitionId,
    string Name,
    string TaskKey,
    bool Enabled,
    ScheduleKind Kind,
    string ScheduleExpression,
    string ScheduleExplanation,
    string TimeZoneId,
    SchedulerMisfirePolicy MisfirePolicy,
    DateTimeOffset? PreviousFireUtc,
    DateTimeOffset? NextFireUtc,
    long Version,
    string UpdatedBy,
    DateTimeOffset UpdatedAtUtc);

public sealed record TaskRunSummaryDto(
    Guid RunId,
    Guid OccurrenceId,
    Guid AttemptId,
    Guid? ScheduleDefinitionId,
    string TaskKey,
    ScheduledRunState State,
    ScheduledRunOrigin Origin,
    DateTimeOffset ScheduledFireUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? FinishedAtUtc,
    int? ProcessId,
    int? ExitCode,
    string? Detail);

public enum SchedulerServiceState
{
    Starting,
    Ready,
    Degraded,
    Unhealthy,
    Stopping
}

public enum ScheduleKind
{
    OneTime,
    SimpleInterval,
    Cron
}

public enum SchedulerMisfirePolicy
{
    DoNothing,
    FireOnceNow
}

public enum SchedulerRiskClassification
{
    Maintenance,
    MarketLifecycle,
    Backup,
    TradingSensitive
}

public enum ScheduledRunOrigin
{
    Scheduled,
    Manual,
    MisfireRecovery,
    Retry
}

public enum ScheduledRunState
{
    Planned,
    BlockedDependency,
    SkippedOverlap,
    Misfired,
    Starting,
    Running,
    Succeeded,
    Failed,
    TimedOut,
    Cancelling,
    Cancelled,
    ForceTerminated,
    Abandoned
}

public enum ScheduledTaskStopMode
{
    None,
    CloseMainWindow,
    StandardInput
}
