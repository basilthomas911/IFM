using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Projection;

/// <summary>Recovers history for both Functions and Commands after commit, without touching financial balances.</summary>
public sealed class FinancialHistoryRecoveryService(FinancialHistoryJournal journal, IFinancialHistoryProjection projection,
    ILogger<FinancialHistoryRecoveryService> logger) : BackgroundService
{
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This actor/service boundary records the exception and contains the failure according to its existing lifecycle or message contract.")]     protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var pending = await (journal.PendingAsync(stoppingToken)).ConfigureAwait(false);
                var applied = 0;
                foreach (var completed in pending)
                {
                    try
                    {
                        await (projection.ApplyAsync(completed, stoppingToken)).ConfigureAwait(false);
                        await (journal.AcknowledgeAsync(completed.EventId, stoppingToken)).ConfigureAwait(false);
                        applied++;
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                    catch (Exception error) { global::TomasAI.IFM.Domain.Portfolio.ActorLifecycleDiagnosticLog.Write1(logger, error, nameof(FinancialHistoryRecoveryService), nameof(ExecuteAsync), completed.EventId); }
                }
                if (pending.Count == 32 && applied == 32) continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception error) { global::TomasAI.IFM.Domain.Portfolio.ActorLifecycleDiagnosticLog.Write2(logger, error, nameof(FinancialHistoryRecoveryService), nameof(ExecuteAsync)); }
            try { await (Task.Delay(TimeSpan.FromSeconds(2), stoppingToken)).ConfigureAwait(false); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
        }
    }
}
