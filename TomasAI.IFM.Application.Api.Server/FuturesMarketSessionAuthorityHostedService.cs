using TomasAI.IFM.Domain.MarketData.Query;

namespace TomasAI.IFM.Application.Api.Server;

/// <summary>
/// Keeps the API process's authoritative futures-session snapshot current at
/// market open/close boundaries and during bounded clock reconciliation.
/// </summary>
internal sealed class FuturesMarketSessionAuthorityHostedService(
    FuturesMarketSessionAuthority authority,
    TimeProvider timeProvider,
    ILogger<FuturesMarketSessionAuthorityHostedService> logger) : BackgroundService
{
    static readonly TimeSpan ReconciliationInterval = TimeSpan.FromMinutes(1);
    static readonly TimeSpan BoundarySettleDelay = TimeSpan.FromMilliseconds(100);

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        var snapshot = authority.Refresh();
        logger.LogInformation(
            "Authoritative futures session initialized at revision {Revision}: operational {OperationalValueDate}, active {ActiveValueDate}, state {MarketState}, next transition {NextTransitionUtc}.",
            snapshot.Revision,
            snapshot.OperationalValueDate,
            snapshot.ActiveValueDate,
            snapshot.State,
            snapshot.NextTransitionUtc);
        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var current = authority.Current;
            var now = timeProvider.GetUtcNow();
            var transition = new DateTimeOffset(
                DateTime.SpecifyKind(current.NextTransitionUtc, DateTimeKind.Utc));
            var untilTransition = transition - now + BoundarySettleDelay;
            var delay = untilTransition <= TimeSpan.Zero
                ? BoundarySettleDelay
                : untilTransition < ReconciliationInterval
                    ? untilTransition
                    : ReconciliationInterval;
            await Task.Delay(delay, timeProvider, stoppingToken).ConfigureAwait(false);

            var previousRevision = current.Revision;
            var refreshed = authority.Refresh();
            if (refreshed.Revision != previousRevision)
            {
                logger.LogInformation(
                    "Authoritative futures session advanced to revision {Revision}: operational {OperationalValueDate}, active {ActiveValueDate}, state {MarketState}, next transition {NextTransitionUtc}.",
                    refreshed.Revision,
                    refreshed.OperationalValueDate,
                    refreshed.ActiveValueDate,
                    refreshed.State,
                    refreshed.NextTransitionUtc);
            }
        }
    }
}
