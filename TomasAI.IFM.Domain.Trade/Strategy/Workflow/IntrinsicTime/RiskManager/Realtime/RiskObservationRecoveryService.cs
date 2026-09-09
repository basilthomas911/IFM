using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Realtime;

/// <summary>Rebuildable committed history and terminal Fund reconciliation; never performs financial mutations directly.</summary>
public sealed class RiskObservationRecoveryService(RiskHistoryJournal journal, IDbContextFactory db,
    IPortfolioEventStore funds, IActorService actors, ILogger<RiskObservationRecoveryService> logger) : BackgroundService
{
    long _after;
    long _fundAfter;
    bool _cursorLoaded;
    public async Task<long> ProjectPageAsync(long after, bool synchronize, CancellationToken token)
    {
        var page = await journal.PageAsync(after, token);
        Exception? projectionFailure=null;
        foreach (var snapshot in page)
        {
            try { await db.TradeDb.UpsertRiskHistoryAsync(snapshot, token); }
            catch(OperationCanceledException) when(token.IsCancellationRequested){throw;}
            catch(Exception e){projectionFailure ??=e;}
            if (synchronize)
            {
                try { await SynchronizeAsync(snapshot, token); }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (Exception e) { logger.LogWarning(e, "Fund outcome for workflow {WorkflowId} awaits reconciliation.", snapshot.WorkflowId); }
            }
        }
        if(projectionFailure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(projectionFailure).Throw();
        return page.Count < 32 ? 0 : page[^1].EventId;
    }
    async Task<long> SynchronizePageAsync(long after,CancellationToken token)
    {
        var page=await journal.PageAsync(after,token);
        foreach(var snapshot in page)
        {
            try {await SynchronizeAsync(snapshot,token);}
            catch(OperationCanceledException) when(token.IsCancellationRequested){throw;}
            catch(Exception e){logger.LogWarning(e,"Fund outcome for workflow {WorkflowId} awaits reconciliation.",snapshot.WorkflowId);}
        }
        return page.Count<32 ? 0 : page[^1].EventId;
    }
    public async Task SynchronizeAsync(WorkflowStrategyStateUpdatedEvent snapshot, CancellationToken token)
    {
        if (snapshot.TerminalRisk is not { } evidence) return;
        var id = new PortfolioFundId(evidence.PortfolioId, evidence.FundId);
        var aggregate = await funds.LoadFundAsync(id, token);
        var order = aggregate.Composition(evidence.OrderId).Order;
        if (order.TerminalRisk == evidence) return;
        if (order.RiskAuthorization is not null) throw new InvalidOperationException("Fund authorization requires explicit financial reconciliation.");
        var command = new PortfolioCommand<SynchronizeFundRiskOutcomePayload, PortfolioFundId>
        {
            CommandId = RiskFinancialHandoff.Identity(evidence.SourceCommandId, $"FundTerminal/{order.AggregateVersion}"),
            EntityId = id, Subject = new(ActorType.Command, PortfolioCommandSubjects.FundActor, PortfolioCommandVerbs.SynchronizeFundRiskOutcome, id.Format()),
            ErrorCode = 34100, CorrelationId = snapshot.CorrelationId, RequestedOnUtc = evidence.DecidedAtUtc,
            Access = PortfolioAccessContext.Workflow("RiskOutcomeRecovery"), Payload = new(order.AggregateVersion, evidence)
        };
        var result = await actors.RequestAsync<PortfolioCommand<SynchronizeFundRiskOutcomePayload, PortfolioFundId>, PortfolioFundId>(command, token);
        if (!result.Success) throw new InvalidOperationException(result.ErrorMessage);
        if ((await funds.LoadFundAsync(id, token)).Composition(evidence.OrderId).Order.TerminalRisk != evidence)
            throw new InvalidOperationException("Fund outcome acknowledgement is not yet authoritative.");
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if(!_cursorLoaded){_after=await journal.LoadCursorAsync(stoppingToken);_fundAfter=await journal.LoadCursorAsync(stoppingToken,"risk-fund-outcomes-v1");_cursorLoaded=true;}
                var fundNext=await SynchronizePageAsync(_fundAfter,stoppingToken);
                await journal.SaveCursorAsync(fundNext,stoppingToken,"risk-fund-outcomes-v1");_fundAfter=fundNext;
                var next=await ProjectPageAsync(_after,false,stoppingToken);
                await journal.SaveCursorAsync(next,stoppingToken);_after=next;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception e) { logger.LogWarning(e, "Risk history projection awaits repair; cursor retained at {Cursor}.", _after); }
            try { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
        }
    }
}
