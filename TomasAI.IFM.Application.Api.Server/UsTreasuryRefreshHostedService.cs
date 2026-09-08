using TomasAI.IFM.Application.MarketData.FinancialModelingPrep;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Framework.MarketData.ReferenceData;

namespace TomasAI.IFM.Application.Api.Server;

/// <summary>Submits durable imports and warms pricing asynchronously after actor bootstrap.</summary>
public sealed class UsTreasuryRefreshHostedService(
    IApplicationBootstrapReadiness readiness,
    IFmpMarketDataImportCoordinator imports,
    TreasuryPricingProvider pricing,
    TreasuryPublicationPolicy publication,
    TimeProvider clock,
    ILogger<UsTreasuryRefreshHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeSpan.FromMinutes(5);
            try
            {
                if (!await readiness.IsHealthyAsync(stoppingToken).ConfigureAwait(false))
                    delay = TimeSpan.FromSeconds(10);
                else
                {
                    var required = publication.RequiredValueDate(clock.GetUtcNow());
                    // Submission is not completion. The existing terminal event -> DownloadLog command ->
                    // durable projector path records acquisition and persistence, including provider failure.
                    var submitted = await imports.ImportAsync(new(required, required, IncludeEconomicCalendar: false),
                        stoppingToken).ConfigureAwait(false);
                    logger.LogInformation("Official Treasury import for {ValueDate}: {Submitted} submitted, {Rejected} rejected. Completion is recorded in DownloadLog under USTreasury.",
                        required, submitted.SubmittedCommands, submitted.RejectedSubmissions);
                    var warmed = await pricing.GetAsync(clock.GetUtcNow(), 0, publication, UsTreasuryCurve.ConversionPolicy, stoppingToken).ConfigureAwait(false);
                    // A response first observed after the frozen valuation is usable on the next valuation.
                    if (warmed.Error == "TreasuryStale")
                        warmed = await pricing.GetAsync(clock.GetUtcNow(), 0, publication, UsTreasuryCurve.ConversionPolicy, stoppingToken).ConfigureAwait(false);
                    if (!warmed.Succeeded)
                        logger.LogWarning("Official Treasury pricing warm-up is unavailable: {Reason}.", warmed.Error);
                    else if (submitted.RejectedSubmissions == 0)
                        delay = TimeSpan.FromMinutes(30);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception error)
            {
                logger.LogWarning(error, "Official Treasury refresh failed; the worker will retry and pricing retains its freshness checks.");
            }
            try { await Task.Delay(delay, clock, stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
        }
    }
}
