using MessagePack;

namespace TomasAI.IFM.Domain.Application.Shared;

/// <summary>Identifies the ordered activities in the application startup workflow.</summary>
public enum ApplicationStartupActivity
{
    ResolveAuthority = 1,
    ReconcileReferenceData = 2,
    ReconcileCurrentContracts = 3,
    StartMarketData = 4,
    WarmHistoricalAnalytics = 5,
    StartRealtimeAnalytics = 6,
    QualifyOperationalState = 7
}

/// <summary>Describes the terminal result of one startup activity.</summary>
public enum ApplicationStartupActivityOutcome
{
    Started = 1,
    AlreadySatisfied = 2,
    ScheduledStopped = 3,
    Degraded = 4,
    Failed = 5,
    SkippedDependency = 6
}

/// <summary>Describes the aggregate state of the latest startup workflow.</summary>
public enum ApplicationLifecycleState
{
    Bootstrapped = 1,
    Starting = 2,
    Running = 3,
    Degraded = 4,
    Failed = 5,
    ScheduledStopped = 6
}

/// <summary>Stable identity and correlation supplied to every startup activity.</summary>
public sealed record ApplicationStartupContext(
    DateOnly ValueDate,
    Guid ProcessBootId,
    Guid CommandId,
    Guid CorrelationId);

/// <summary>Typed, bounded terminal result for one startup activity.</summary>
[MessagePackObject]
public sealed record ApplicationStartupActivityResult
{
    [Key(0)] public ApplicationStartupActivity Activity { get; init; }
    [Key(1)] public ApplicationStartupActivityOutcome Outcome { get; init; }
    [Key(2)] public bool Required { get; init; }
    [Key(3)] public DateTime StartedAtUtc { get; init; }
    [Key(4)] public DateTime CompletedAtUtc { get; init; }
    [Key(5)] public int ErrorCode { get; init; }
    [Key(6)] public string Reason { get; init; } = string.Empty;

    [IgnoreMember]
    public bool IsSatisfied => Outcome is ApplicationStartupActivityOutcome.Started
        or ApplicationStartupActivityOutcome.AlreadySatisfied
        or ApplicationStartupActivityOutcome.ScheduledStopped;
}

/// <summary>Queryable process-local snapshot of the latest application startup.</summary>
[MessagePackObject]
public sealed record ApplicationStartupStatus
{
    [Key(0)] public ApplicationLifecycleState State { get; init; }
    [Key(1)] public DateOnly ValueDate { get; init; }
    [Key(2)] public Guid ProcessBootId { get; init; }
    [Key(3)] public Guid CommandId { get; init; }
    [Key(4)] public Guid CorrelationId { get; init; }
    [Key(5)] public DateTime StartedAtUtc { get; init; }
    [Key(6)] public DateTime? CompletedAtUtc { get; init; }
    [Key(7)] public ApplicationStartupActivityResult[] Activities { get; init; } = [];
    [Key(8)] public string Summary { get; init; } = string.Empty;
}

/// <summary>
/// Actor-facing operational adapters. Implementations perform one bounded activity only;
/// ordering, exception conversion, reporting, and aggregation belong to the Application event actor.
/// </summary>
public interface IApplicationStartupActivities
{
    ValueTask<ApplicationStartupActivityOutcome> ResolveAuthorityAsync(
        ApplicationStartupContext context,
        CancellationToken cancellationToken);
    ValueTask<ApplicationStartupActivityOutcome> ReconcileReferenceDataAsync(
        ApplicationStartupContext context,
        CancellationToken cancellationToken);
    ValueTask<ApplicationStartupActivityOutcome> ReconcileCurrentContractsAsync(
        ApplicationStartupContext context,
        CancellationToken cancellationToken);
    ValueTask<ApplicationStartupActivityOutcome> StartMarketDataAsync(
        ApplicationStartupContext context,
        CancellationToken cancellationToken);
    ValueTask<ApplicationStartupActivityOutcome> WarmHistoricalAnalyticsAsync(
        ApplicationStartupContext context,
        CancellationToken cancellationToken);
    ValueTask<ApplicationStartupActivityOutcome> StartRealtimeAnalyticsAsync(
        ApplicationStartupContext context,
        CancellationToken cancellationToken);
    ValueTask<ApplicationStartupActivityOutcome> QualifyOperationalStateAsync(
        ApplicationStartupContext context,
        CancellationToken cancellationToken);
}

/// <summary>Read/write boundary for the latest startup workflow snapshot.</summary>
public interface IApplicationStartupStatusStore
{
    ApplicationStartupStatus Current { get; }
    void Set(ApplicationStartupStatus status);
}

/// <summary>One immutable step in the authoritative sequential startup plan.</summary>
public sealed record ApplicationStartupActivityDefinition(
    ApplicationStartupActivity Activity,
    bool Required,
    IReadOnlyCollection<ApplicationStartupActivity> Dependencies);

/// <summary>Authoritative activity order, dependencies, and aggregate policy.</summary>
public static class ApplicationStartupPlan
{
    public static IReadOnlyList<ApplicationStartupActivityDefinition> Activities { get; } =
    [
        new(ApplicationStartupActivity.ResolveAuthority, true, []),
        new(ApplicationStartupActivity.ReconcileReferenceData, false, [ApplicationStartupActivity.ResolveAuthority]),
        new(ApplicationStartupActivity.ReconcileCurrentContracts, true, [ApplicationStartupActivity.ResolveAuthority]),
        new(ApplicationStartupActivity.WarmHistoricalAnalytics, false, [ApplicationStartupActivity.ReconcileCurrentContracts]),
        new(ApplicationStartupActivity.StartRealtimeAnalytics, true, [ApplicationStartupActivity.ReconcileCurrentContracts]),
        new(ApplicationStartupActivity.StartMarketData, true,
        [
            ApplicationStartupActivity.ReconcileCurrentContracts,
            ApplicationStartupActivity.StartRealtimeAnalytics
        ]),
        new(ApplicationStartupActivity.QualifyOperationalState, true,
        [
            ApplicationStartupActivity.StartRealtimeAnalytics,
            ApplicationStartupActivity.StartMarketData
        ])
    ];

    public static ApplicationLifecycleState Aggregate(
        IReadOnlyCollection<ApplicationStartupActivityResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        if (results.Any(result => result.Required && !result.IsSatisfied))
            return ApplicationLifecycleState.Failed;
        if (results.Any(result => !result.Required && !result.IsSatisfied))
            return ApplicationLifecycleState.Degraded;
        if (results.Any(result => result.Outcome == ApplicationStartupActivityOutcome.ScheduledStopped))
            return ApplicationLifecycleState.ScheduledStopped;
        return ApplicationLifecycleState.Running;
    }
}
