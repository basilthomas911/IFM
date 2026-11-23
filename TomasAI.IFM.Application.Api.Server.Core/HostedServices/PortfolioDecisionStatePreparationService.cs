using TomasAI.IFM.Domain.Portfolio.OrderComposition.Model;
using TomasAI.IFM.Domain.Portfolio.OrderComposition.Repository;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;

namespace TomasAI.IFM.Application.Api.Server.Core.HostedServices;

/// <summary>Prepares authoritative cash/capacity generations independently of the strategy decision runner.</summary>
public sealed partial class PortfolioDecisionStatePreparationService(PortfolioOrderCompositionStore store,
    PortfolioDecisionStateCache cache, IntrinsicTimeStrategyWorkflowOptions options,
    ILogger<PortfolioDecisionStatePreparationService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        do
        {
            if (!options.Enabled) continue;
            foreach (var portfolio in options.Assignments.Select(value => value.PortfolioId).Append(options.PortfolioId).Distinct())
            {
                try { cache.Publish(await store.ReadDecisionStateAsync(portfolio, stoppingToken).ConfigureAwait(false)); }
                catch (Exception error) when (!stoppingToken.IsCancellationRequested) { StateUnavailable(logger, portfolio, error); }
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
    [LoggerMessage(Level = LogLevel.Warning, Message = "Financial decision inputs unavailable for portfolio {PortfolioId}.")]
    static partial void StateUnavailable(ILogger logger, int portfolioId, Exception error);
}
