using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Projection;

/// <summary>Recovers history for both Functions and Commands after commit, without touching financial balances.</summary>
public sealed class FinancialHistoryRecoveryService(FinancialHistoryJournal journal,IFinancialHistoryProjection projection,
    ILogger<FinancialHistoryRecoveryService> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while(!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var pending=await journal.PendingAsync(stoppingToken);
                var applied=0;
                foreach(var completed in pending)
                {
                    try
                    {
                        await projection.ApplyAsync(completed,stoppingToken);
                        await journal.AcknowledgeAsync(completed.EventId,stoppingToken);
                        applied++;
                    }
                    catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested) { return; }
                    catch(Exception error) { logger.LogWarning(error,"Financial history event {EventId} remains pending.",completed.EventId); }
                }
                if(pending.Count==32 && applied==32) continue;
            }
            catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested) { return; }
            catch(Exception error) { logger.LogWarning(error,"Financial history projection is pending; PostgreSQL receipts remain authoritative."); }
            try { await Task.Delay(TimeSpan.FromSeconds(2),stoppingToken); }
            catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested) { return; }
        }
    }
}
