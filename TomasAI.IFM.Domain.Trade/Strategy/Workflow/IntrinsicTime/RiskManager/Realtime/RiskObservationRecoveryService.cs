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

public interface IWorkflowRiskProjection
{
    Task ProjectCommittedAsync(WorkflowStrategyStateUpdatedEvent snapshot, CancellationToken token);
}

/// <summary>Projects delivered workflow events and exposes bounded recovery operations for explicit repair.</summary>
public sealed class RiskObservationRecoveryService(RiskHistoryJournal journal, IDbContextFactory db,
    IPortfolioEventStore funds, IActorService actors, ILogger<RiskObservationRecoveryService> logger)
    : IWorkflowRiskProjection
{
    readonly SemaphoreSlim serial = new(1, 1);

    public async Task ProjectCommittedAsync(WorkflowStrategyStateUpdatedEvent snapshot, CancellationToken token)
    {
        await serial.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var result = await db.TradeDb.UpsertRiskHistoryAsync(snapshot, token).ConfigureAwait(false);
            if (result.Disposition == RiskHistoryProjectionDisposition.Conflict)
            {
                await journal.QuarantineConflictAsync(snapshot.EventId, result, token).ConfigureAwait(false);
                logger.LogError(
                    "Risk history source conflict quarantined. EventId={EventId} WorkflowId={WorkflowId} InvocationId={InvocationId} Revision={Revision}; the immutable stored definition was preserved.",
                    snapshot.EventId, result.WorkflowId, result.InvocationId, result.Revision);
            }
            await SynchronizeAsync(snapshot, token).ConfigureAwait(false);
        }
        finally { serial.Release(); }
    }
    public async Task<long> ProjectPageAsync(long after, bool synchronize, CancellationToken token)
    {
        var page = await journal.PageAsync(after, token, RiskHistoryJournal.HistoryProjection);
        foreach (var snapshot in page)
        {
            var result = await db.TradeDb.UpsertRiskHistoryAsync(snapshot, token);
            if (result.Disposition == RiskHistoryProjectionDisposition.Conflict)
            {
                await journal.QuarantineConflictAsync(snapshot.EventId, result, token);
                logger.LogError(
                    "Risk history source conflict quarantined. EventId={EventId} WorkflowId={WorkflowId} InvocationId={InvocationId} Revision={Revision}; the immutable stored definition was preserved.",
                    snapshot.EventId, result.WorkflowId, result.InvocationId, result.Revision);
            }
            else
                await journal.AcknowledgeAsync(RiskHistoryJournal.HistoryProjection, snapshot.EventId, token);

            if (synchronize)
                await SynchronizeAndAcknowledgeAsync(snapshot, token);
        }
        return Advance(after, page.Select(snapshot=>snapshot.EventId).ToArray());
    }

    async Task<long> SynchronizePageAsync(long after,CancellationToken token)
    {
        var page=await journal.PageAsync(after,token,RiskHistoryJournal.FundOutcomeProjection);
        foreach(var snapshot in page)
            await SynchronizeAndAcknowledgeAsync(snapshot,token);
        return Advance(after,page.Select(snapshot=>snapshot.EventId).ToArray());
    }

    async Task SynchronizeAndAcknowledgeAsync(WorkflowStrategyStateUpdatedEvent snapshot,CancellationToken token)
    {
        try
        {
            await SynchronizeAsync(snapshot,token);
            await journal.AcknowledgeAsync(RiskHistoryJournal.FundOutcomeProjection,snapshot.EventId,token);
        }
        catch(OperationCanceledException) when(token.IsCancellationRequested){throw;}
        catch(Exception e)
        {
            logger.LogWarning(e,"Fund outcome for workflow {WorkflowId} awaits reconciliation.",snapshot.WorkflowId);
        }
    }

    internal static long Advance(long after,IReadOnlyList<long> eventIds)
        =>eventIds.Count==0?after:Math.Max(after,eventIds.Max());
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
}
