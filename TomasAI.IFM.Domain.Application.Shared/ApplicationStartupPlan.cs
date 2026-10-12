using MessagePack;

namespace TomasAI.IFM.Domain.Application.Shared;

/// <summary>Authoritative activity order, dependencies, and aggregate policy.</summary>
public static class ApplicationStartupPlan
{
    public static IReadOnlyList<ApplicationStartupActivityDefinition> Activities { get; } =
    [
        new(ApplicationStartupActivity.ResolveAuthority, true, []),
        new(ApplicationStartupActivity.ApplyParameterSets, false, [ApplicationStartupActivity.ResolveAuthority]),
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
