using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Development;

/// <summary>Refreshes committed fund authority outside the ITI signal hot path.</summary>
public sealed partial class DevelopmentWorkflowAssignmentRefreshService(
    DevelopmentTradingPortfolioProvisioner provisioner,
    IntrinsicTimeStrategyWorkflowOptions workflow,
    ILogger<DevelopmentWorkflowAssignmentRefreshService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(4));
            try
            {
                await provisioner.RefreshAssignmentsAsync(DateTime.UtcNow, timeout.Token).ConfigureAwait(false);
            }
            catch (Exception error) when (!stoppingToken.IsCancellationRequested)
            {
                workflow.SetAssignments([]);
                AssignmentRefreshFailed(logger, error);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Development ITI assignment cache refresh failed.")]
    private static partial void AssignmentRefreshFailed(ILogger logger, Exception error);
}
