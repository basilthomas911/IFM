using System.Text.Json;

namespace TomasAI.IFM.Application.ServerManager.Contracts;

public static class SchedulerProtocol
{
    public const int Version = 1;
    public const int MaximumFrameBytes = 1024 * 1024;
    public const string GetDashboardOperation = "scheduler.dashboard.get";
    public const string ValidateScheduleOperation = "scheduler.schedule.validate";
    public const string CreateScheduleOperation = "scheduler.schedule.create";
    public const string UpdateScheduleOperation = "scheduler.schedule.update";
    public const string SetScheduleEnabledOperation = "scheduler.schedule.set-enabled";
    public const string DeleteScheduleOperation = "scheduler.schedule.delete";
    public const string RunNowOperation = "scheduler.run.start";
    public const string CancelRunOperation = "scheduler.run.cancel";
    public const string RetryRunOperation = "scheduler.run.retry";
    public const string GetRunOutputOperation = "scheduler.run.output.get";
    public const string RunRetentionOperation = "scheduler.retention.run";
}

public sealed record SchedulerPipeRequest(
    int Version,
    Guid RequestId,
    string Operation,
    DateTimeOffset OriginatedAtUtc,
    JsonElement? Payload = null,
    long? ExpectedVersion = null,
    string? Reason = null);

public sealed record SchedulerPipeResponse(
    int Version,
    Guid RequestId,
    bool Success,
    string? ErrorCode,
    string? ErrorMessage,
    SchedulerDashboardDto? Dashboard,
    ScheduleValidationResultDto? Validation = null,
    SchedulerOperationResultDto? OperationResult = null,
    TaskRunOutputPageDto? OutputPage = null);

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
    DateTimeOffset UpdatedAtUtc,
    string Description = "",
    int? MaximumRuntimeSeconds = null,
    int SuccessfulRetentionDays = 30,
    int FailedRetentionDays = 180);

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

public sealed record ScheduleDefinitionInputDto(
    Guid? ScheduleDefinitionId,
    string Name,
    string Description,
    string TaskKey,
    ScheduleKind Kind,
    string ScheduleExpression,
    string TimeZoneId,
    SchedulerMisfirePolicy MisfirePolicy,
    int? MaximumRuntimeSeconds,
    int SuccessfulRetentionDays = 30,
    int FailedRetentionDays = 180);

public sealed record SetScheduleEnabledDto(Guid ScheduleDefinitionId, bool Enabled);

public sealed record ScheduleIdentityDto(Guid ScheduleDefinitionId);

public sealed record RunIdentityDto(Guid RunId);

public sealed record RunNowRequestDto(Guid ScheduleDefinitionId);

public sealed record RunOutputRequestDto(
    Guid RunId,
    TaskOutputStream Stream,
    long Cursor,
    int PageSize = 200);

public sealed record ScheduleValidationResultDto(
    bool IsValid,
    string Explanation,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<ScheduleFirePreviewDto> NextFireTimes);

public sealed record ScheduleFirePreviewDto(DateTimeOffset Utc, DateTimeOffset Local, string TimeZoneId);

public sealed record SchedulerOperationResultDto(
    string Operation,
    string Message,
    Guid? EntityId,
    long? Version,
    Guid? RunId = null,
    Guid? OccurrenceId = null,
    bool Replayed = false);

public sealed record TaskRunOutputPageDto(
    Guid RunId,
    TaskOutputStream Stream,
    IReadOnlyList<TaskOutputLineDto> Lines,
    long NextCursor,
    bool EndOfStream,
    bool Truncated,
    bool Retained);

public sealed record TaskOutputLineDto(long Sequence, string Text);

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
    StandardInput,
    NamedPipe
}

public enum TaskOutputStream
{
    StandardOutput,
    StandardError
}
