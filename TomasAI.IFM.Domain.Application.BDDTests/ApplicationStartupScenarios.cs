using TomasAI.IFM.Domain.Application.Shared;

namespace TomasAI.IFM.Domain.Application.Actor.BDDTests;

public sealed class ApplicationStartupScenarios
{
    [Fact]
    public void Given_a_healthy_boot_when_startup_is_planned_then_the_feed_starts_after_analytics()
    {
        Assert.Equal(
            [
                ApplicationStartupActivity.ResolveAuthority,
                ApplicationStartupActivity.ReconcileReferenceData,
                ApplicationStartupActivity.ReconcileCurrentContracts,
                ApplicationStartupActivity.WarmHistoricalAnalytics,
                ApplicationStartupActivity.StartRealtimeAnalytics,
                ApplicationStartupActivity.StartMarketData,
                ApplicationStartupActivity.QualifyOperationalState
            ],
            ApplicationStartupPlan.Activities.Select(value => value.Activity));
        Assert.Contains(
            ApplicationStartupActivity.ReconcileCurrentContracts,
            Definition(ApplicationStartupActivity.StartMarketData).Dependencies);
        Assert.Contains(
            ApplicationStartupActivity.StartRealtimeAnalytics,
            Definition(ApplicationStartupActivity.StartMarketData).Dependencies);
        Assert.Contains(
            ApplicationStartupActivity.StartMarketData,
            Definition(ApplicationStartupActivity.QualifyOperationalState).Dependencies);
    }

    [Fact]
    public void Given_an_optional_failure_when_required_work_succeeds_then_startup_is_degraded()
    {
        var results = SuccessfulResults();
        results[Index(ApplicationStartupActivity.ReconcileReferenceData)] = Result(
            ApplicationStartupActivity.ReconcileReferenceData,
            required: false,
            ApplicationStartupActivityOutcome.Failed);

        Assert.Equal(ApplicationLifecycleState.Degraded, ApplicationStartupPlan.Aggregate(results));
    }

    [Fact]
    public void Given_a_required_failure_and_skipped_dependents_then_startup_is_failed()
    {
        var results = SuccessfulResults();
        results[Index(ApplicationStartupActivity.ReconcileCurrentContracts)] = Result(
            ApplicationStartupActivity.ReconcileCurrentContracts,
            required: true,
            ApplicationStartupActivityOutcome.Failed);
        results[Index(ApplicationStartupActivity.StartMarketData)] = Result(
            ApplicationStartupActivity.StartMarketData,
            required: true,
            ApplicationStartupActivityOutcome.SkippedDependency);

        Assert.Equal(ApplicationLifecycleState.Failed, ApplicationStartupPlan.Aggregate(results));
    }

    [Fact]
    public void Given_a_scheduled_close_when_all_work_is_satisfied_then_state_is_scheduled_stopped()
    {
        var results = SuccessfulResults();
        results[Index(ApplicationStartupActivity.StartMarketData)] = Result(
            ApplicationStartupActivity.StartMarketData,
            required: true,
            ApplicationStartupActivityOutcome.ScheduledStopped);
        results[Index(ApplicationStartupActivity.StartRealtimeAnalytics)] = Result(
            ApplicationStartupActivity.StartRealtimeAnalytics,
            required: true,
            ApplicationStartupActivityOutcome.ScheduledStopped);

        Assert.Equal(
            ApplicationLifecycleState.ScheduledStopped,
            ApplicationStartupPlan.Aggregate(results));
    }

    static ApplicationStartupActivityDefinition Definition(ApplicationStartupActivity activity) =>
        ApplicationStartupPlan.Activities.Single(value => value.Activity == activity);

    static int Index(ApplicationStartupActivity activity) =>
        ApplicationStartupPlan.Activities
            .Select((value, index) => (value.Activity, index))
            .Single(value => value.Activity == activity).index;

    static ApplicationStartupActivityResult[] SuccessfulResults() =>
        ApplicationStartupPlan.Activities.Select(value => Result(
            value.Activity,
            value.Required,
            ApplicationStartupActivityOutcome.AlreadySatisfied)).ToArray();

    static ApplicationStartupActivityResult Result(
        ApplicationStartupActivity activity,
        bool required,
        ApplicationStartupActivityOutcome outcome) => new()
        {
            Activity = activity,
            Required = required,
            Outcome = outcome,
            StartedAtUtc = DateTime.UtcNow,
            CompletedAtUtc = DateTime.UtcNow
        };
}
