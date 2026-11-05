using TomasAI.IFM.Application.MarketData;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Application.Api.Server.Core.HostedServices;

/// <summary>Attaches the API-owned analytics for each native market session, independently of provider readiness.</summary>
public sealed class SessionAnalyticsStartupService(IMarketSessionAnalyticsInitializer initializer,
    IFuturesMarketSessionAuthority sessions, IMarketDataApi market, IActorSupervisor supervisor,
    ILogger<SessionAnalyticsStartupService> logger,
    TomasAI.IFM.Application.Api.Server.Core.Startup.Application.StoredSessionIndicatorWarmup? warmup = null) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        DateOnly? initialized = null;
        DateOnly? warmed = null;
        var nextWarmupAttempt = DateTimeOffset.MinValue;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        try
        {
            do
            {
                var date = sessions.Current.ActiveValueDate;
                if (date is null) { initialized = null; warmed = null; continue; }
                if (!supervisor.IsReady || !market.IsDatabentoFeedUp()) continue;
                try
                {
                    if (initialized != date)
                    {
                        await initializer.InitializeAsync(date.Value, stoppingToken).ConfigureAwait(false);
                        initialized = date;
                        logger.LogInformation("Session analytics attached for {ValueDate}; signal readiness remains independently qualified.", date);
                    }
                    if (warmed != date && warmup is not null && DateTimeOffset.UtcNow >= nextWarmupAttempt)
                    {
                        nextWarmupAttempt = DateTimeOffset.UtcNow.AddMinutes(1);
                        if (await warmup.WarmAsync(date.Value, stoppingToken).ConfigureAwait(false))
                        {
                            warmed = date;
                            logger.LogInformation("Strategy indicator warm-up completed for {ValueDate}.", date);
                        }
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch (Exception error)
                {
                    logger.LogError(error, "Session analytics initialization failed for {ValueDate}; attachment remains retryable.", date);
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
