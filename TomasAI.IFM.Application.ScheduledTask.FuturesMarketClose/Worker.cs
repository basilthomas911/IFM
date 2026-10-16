using TomasAI.IFM.Application.ScheduledTask.Shared;
using TomasAI.IFM.Domain.SystemAdmin.Shared.MarketClose;

namespace TomasAI.IFM.Application.ScheduledTask.FuturesMarketClose;

/// <summary>Requests backend-owned market close; owns no business dates, positions, or backups.</summary>
public sealed class Worker(IHostApplicationLifetime lifetime, ScheduledTaskOutcome outcome,
    ILogger<Worker> logger, IMarketCloseApi marketClose)
    : OneShotScheduledTaskWorker(lifetime, outcome, logger)
{
    protected override async Task ExecuteTaskAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Market Close requesting the API-owned current session close.");
        var result = await marketClose.CloseMarketAsync().WaitAsync(stoppingToken).ConfigureAwait(false);
        logger.LogInformation("Market Close confirmed ValueDate={ValueDate} RunId={RunId} Positions={Positions} AlreadyCompleted={AlreadyCompleted}",
            result.ValueDate, result.RunId, result.PositionsFinalized, result.AlreadyCompleted);
    }
}
