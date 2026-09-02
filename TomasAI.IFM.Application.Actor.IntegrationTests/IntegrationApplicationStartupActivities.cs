using TomasAI.IFM.Domain.Application.Shared;

namespace TomasAI.IFM.Application.Actor.IntegrationTests;

internal sealed class IntegrationApplicationStartupActivities : IApplicationStartupActivities
{
    static ValueTask<ApplicationStartupActivityOutcome> Satisfied(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(ApplicationStartupActivityOutcome.AlreadySatisfied);
    }

    public ValueTask<ApplicationStartupActivityOutcome> ResolveAuthorityAsync(ApplicationStartupContext context, CancellationToken cancellationToken) => Satisfied(cancellationToken);
    public ValueTask<ApplicationStartupActivityOutcome> ReconcileReferenceDataAsync(ApplicationStartupContext context, CancellationToken cancellationToken) => Satisfied(cancellationToken);
    public ValueTask<ApplicationStartupActivityOutcome> ReconcileCurrentContractsAsync(ApplicationStartupContext context, CancellationToken cancellationToken) => Satisfied(cancellationToken);
    public ValueTask<ApplicationStartupActivityOutcome> StartMarketDataAsync(ApplicationStartupContext context, CancellationToken cancellationToken) => Satisfied(cancellationToken);
    public ValueTask<ApplicationStartupActivityOutcome> WarmHistoricalAnalyticsAsync(ApplicationStartupContext context, CancellationToken cancellationToken) => Satisfied(cancellationToken);
    public ValueTask<ApplicationStartupActivityOutcome> StartRealtimeAnalyticsAsync(ApplicationStartupContext context, CancellationToken cancellationToken) => Satisfied(cancellationToken);
    public ValueTask<ApplicationStartupActivityOutcome> QualifyOperationalStateAsync(ApplicationStartupContext context, CancellationToken cancellationToken) => Satisfied(cancellationToken);
}
