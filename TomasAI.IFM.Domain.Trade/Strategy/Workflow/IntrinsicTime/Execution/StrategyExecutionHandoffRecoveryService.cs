using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Execution.Projection;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Execution;

/// <summary>Fair bounded recovery of accepted openings, independent of whether new admission is enabled.</summary>
public sealed class StrategyExecutionHandoffRecoveryService(IActorService actors, IDbContextFactory databases,
    IPortfolioFinancialApi financial, IPostgresEventTransaction transactions, TimeProvider clock, IPortfolioQueryApi portfolio,
    ILogger<StrategyExecutionHandoffRecoveryService> logger,
    TomasAI.IFM.Domain.Portfolio.GeneralLedger.IPortfolioTradeAccountingApi accounting) : BackgroundService
{
    string after = string.Empty;
    readonly StrategyExecutionHandoffReadStore store = new(transactions);

    public async Task RunOnceAsync(CancellationToken token)
    {
        var page = await store.PendingAsync(after, token).ConfigureAwait(false);
        foreach (var value in page)
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20), clock);
            using var step = CancellationTokenSource.CreateLinkedTokenSource(token, deadline.Token);
            try
            {
                await new StrategyExecutionHandoffProcessor(actors, databases, financial, store, clock, portfolio,
                    accounting).StepAsync(value, step.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                logger.LogWarning("Strategy opening {HandoffId} remains pending after the reconciliation deadline.", value.Id.Format());
            }
            after = value.Id.Format();
        }
        if (page.Count < 32) after = string.Empty;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunOnceAsync(stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception error) { logger.LogWarning(error, "Strategy opening reconciliation remains pending."); }
            try { await Task.Delay(TimeSpan.FromSeconds(2), clock, stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
        }
    }
}
