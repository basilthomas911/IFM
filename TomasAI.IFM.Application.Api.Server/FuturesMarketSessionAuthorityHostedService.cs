using TomasAI.IFM.Domain.MarketData.Query;

namespace TomasAI.IFM.Application.Api.Server;

/// <summary>
/// Keeps the API process's authoritative futures-session snapshot current at
/// market open/close boundaries and during bounded clock reconciliation.
/// </summary>
public sealed class FuturesMarketSessionAuthorityHostedService(
    FuturesMarketSessionAuthority authority,
    TimeProvider timeProvider,
    ILogger<FuturesMarketSessionAuthorityHostedService> logger) : BackgroundService
{
    static readonly TimeSpan ReconciliationInterval = TimeSpan.FromMinutes(1);
    static readonly TimeSpan BoundarySettleDelay = TimeSpan.FromMilliseconds(100);

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = authority.Refresh();
            logger.LogInformation(
                "Authoritative futures session initialized at revision {Revision}: operational {OperationalValueDate}, active {ActiveValueDate}, state {MarketState}, next transition {NextTransitionUtc}.",
                snapshot.Revision,
                snapshot.OperationalValueDate,
                snapshot.ActiveValueDate,
                snapshot.State,
                snapshot.NextTransitionUtc);
            await base.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Futures market-session authority startup was cancelled by API shutdown.");
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Futures market-session authority failed to start; the API host will remain running.");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
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
                if (!await HostedServiceLifecycle.DelayAsync(
                        delay, timeProvider, stoppingToken).ConfigureAwait(false))
                {
                    return;
                }

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
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Futures market-session authority stopped during API shutdown.");
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Futures market-session authority failed unexpectedly; the API host will remain running.");
        }
    }
}
