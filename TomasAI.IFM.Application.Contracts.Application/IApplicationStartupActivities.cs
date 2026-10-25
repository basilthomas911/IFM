using MessagePack;

namespace TomasAI.IFM.Domain.Application.Shared;

/// <summary>
/// Actor-facing operational adapters. Implementations perform one bounded activity only;
/// ordering, exception conversion, reporting, and aggregation belong to the Application event actor.
/// </summary>
public interface IApplicationStartupActivities
{
    ValueTask<ApplicationStartupActivityOutcome> ApplyParameterSetsAsync(ApplicationStartupContext context, CancellationToken cancellationToken)
        => ValueTask.FromResult(ApplicationStartupActivityOutcome.AlreadySatisfied);
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
