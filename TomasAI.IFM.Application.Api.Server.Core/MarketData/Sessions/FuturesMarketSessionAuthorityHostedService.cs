using TomasAI.IFM.Application.Api.Server.Core.Hosting;
using TomasAI.IFM.Domain.MarketData.Query;

namespace TomasAI.IFM.Application.Api.Server.Core.MarketData.Sessions;

/// <summary>
/// Keeps the API process's authoritative futures-session snapshot current at
/// market open/close boundaries and during bounded clock reconciliation.
/// </summary>
public sealed class FuturesMarketSessionAuthorityHostedService(
    FuturesMarketSessionAuthority authority,
    TimeProvider timeProvider,
    ILogger<FuturesMarketSessionAuthorityHostedService> logger,
    TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts.IScheduledTaskReadStore? taskReads = null,
    IHostEnvironment? environment = null) : BackgroundService
{
    static readonly TimeSpan ReconciliationInterval = TimeSpan.FromMinutes(1);
    static readonly TimeSpan BoundarySettleDelay = TimeSpan.FromMilliseconds(100);

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await RestoreCompletedEndOfDayAsync(cancellationToken).ConfigureAwait(false);
            var snapshot = authority.Refresh();
            logger.LogInformation(
                "{Component}.{Method} "+"Authoritative futures session initialized at revision {Revision}: operational {OperationalValueDate}, active {ActiveValueDate}, state {MarketState}, next transition {NextTransitionUtc}.",nameof(FuturesMarketSessionAuthorityHostedService),nameof(StartAsync),                snapshot.Revision,                snapshot.OperationalValueDate,                snapshot.ActiveValueDate,                snapshot.State,                snapshot.NextTransitionUtc);
            await base.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("{Component}.{Method} "+"Futures market-session authority startup was cancelled by API shutdown.",nameof(FuturesMarketSessionAuthorityHostedService),nameof(StartAsync));
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,                "{Component}.{Method} "+"Futures market-session authority failed to start; the API host will remain running.",nameof(FuturesMarketSessionAuthorityHostedService),nameof(StartAsync));
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

                await RestoreCompletedEndOfDayAsync(stoppingToken).ConfigureAwait(false);
                var previousRevision = current.Revision;
                var refreshed = authority.Refresh();
                if (refreshed.Revision != previousRevision)
                {
                    logger.LogInformation(
                        "{Component}.{Method} "+"Authoritative futures session advanced to revision {Revision}: operational {OperationalValueDate}, active {ActiveValueDate}, state {MarketState}, next transition {NextTransitionUtc}.",nameof(FuturesMarketSessionAuthorityHostedService),nameof(ExecuteAsync),                        refreshed.Revision,                        refreshed.OperationalValueDate,                        refreshed.ActiveValueDate,                        refreshed.State,                        refreshed.NextTransitionUtc);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("{Component}.{Method} "+"Futures market-session authority stopped during API shutdown.",nameof(FuturesMarketSessionAuthorityHostedService),nameof(ExecuteAsync));
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,                "{Component}.{Method} "+"Futures market-session authority failed unexpectedly; the API host will remain running.",nameof(FuturesMarketSessionAuthorityHostedService),nameof(ExecuteAsync));
        }
    }
    /// <summary>Restores the latest persisted successful close, retaining the held date when storage is unavailable.</summary>
    private async Task RestoreCompletedEndOfDayAsync(CancellationToken cancellationToken)
    {
        if (taskReads is null || environment is null) return;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            var completed = await taskReads.GetCompletedEndOfDayAsync(environment.EnvironmentName, deadline.Token).ConfigureAwait(false);
            if (completed is { } valueDate) authority.ApplyCompletedEndOfDay(valueDate);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            logger.LogError(exception, "{Component}.{Method} EOD completion could not be restored for {Environment}; operational value date remains {ValueDate}",
                nameof(FuturesMarketSessionAuthorityHostedService), nameof(RestoreCompletedEndOfDayAsync), environment.EnvironmentName, authority.Current.OperationalValueDate);
        }
    }
}
