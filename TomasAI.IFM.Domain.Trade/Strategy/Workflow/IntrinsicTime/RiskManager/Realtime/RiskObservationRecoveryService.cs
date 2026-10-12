using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Realtime;
/// <summary>Projects delivered workflow events and exposes bounded recovery operations for explicit repair.</summary>
public sealed class RiskObservationRecoveryService(RiskHistoryJournal journal, IDbContextFactory db, IPortfolioEventStore funds, IActorService actors, ILogger<RiskObservationRecoveryService> logger) : IWorkflowRiskProjection, IDisposable
{
    int disposed;
    /// <summary>Releases the owned serialization gate after the host has stopped its workers; safe for DI aliases.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
            serial.Dispose();
    }

    readonly SemaphoreSlim serial = new(1, 1);
    public async Task ProjectCommittedAsync(WorkflowStrategyStateUpdatedEvent snapshot, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        await serial.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var result = string.Equals(db.TradeDb.ProviderName, "System.Data.Postgres", StringComparison.Ordinal)
                ? new RiskHistoryProjectionResult(RiskHistoryProjectionDisposition.AlreadyProjected)
                : await db.TradeDb.UpsertRiskHistoryAsync(snapshot, token).ConfigureAwait(false);
            if (result.Disposition == RiskHistoryProjectionDisposition.Conflict)
            {
                await journal.QuarantineConflictAsync(snapshot.EventId, result, token).ConfigureAwait(false);
                if (logger.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Error))
                    global::TomasAI.IFM.Domain.Trade.TradeBoundaryDiagnosticLog.Write43(logger, nameof(RiskObservationRecoveryService), nameof(ProjectCommittedAsync), snapshot.EventId, result.WorkflowId, result.InvocationId, result.Revision);
            }

            await SynchronizeAsync(snapshot, token).ConfigureAwait(false);
        }
        finally
        {
            serial.Release();
        }
    }

    public async Task<long> ProjectPageAsync(long after, bool synchronize, CancellationToken token)
    {
        var page = await (journal.PageAsync(after, token, RiskHistoryJournal.HistoryProjection)).ConfigureAwait(false);
        foreach (var snapshot in page)
        {
            var result = await (db.TradeDb.UpsertRiskHistoryAsync(snapshot, token)).ConfigureAwait(false);
            if (result.Disposition == RiskHistoryProjectionDisposition.Conflict)
            {
                await (journal.QuarantineConflictAsync(snapshot.EventId, result, token)).ConfigureAwait(false);
                if (logger.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Error))
                    global::TomasAI.IFM.Domain.Trade.TradeBoundaryDiagnosticLog.Write44(logger, nameof(RiskObservationRecoveryService), nameof(ProjectPageAsync), snapshot.EventId, result.WorkflowId, result.InvocationId, result.Revision);
            }
            else
                await (journal.AcknowledgeAsync(RiskHistoryJournal.HistoryProjection, snapshot.EventId, token)).ConfigureAwait(false);
            if (synchronize)
                await (SynchronizeAndAcknowledgeAsync(snapshot, token)).ConfigureAwait(false);
        }

        return Advance(after, page.Select(snapshot => snapshot.EventId).ToArray());
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "A reconciliation delivery failure is logged without acknowledging it; cancellation propagates and the recovery worker remains available.")]
    async Task SynchronizeAndAcknowledgeAsync(WorkflowStrategyStateUpdatedEvent snapshot, CancellationToken token)
    {
        try
        {
            await (SynchronizeAsync(snapshot, token)).ConfigureAwait(false);
            await (journal.AcknowledgeAsync(RiskHistoryJournal.FundOutcomeProjection, snapshot.EventId, token)).ConfigureAwait(false);
        }
        catch (OperationCanceledException)when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            if (logger.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Warning))
                global::TomasAI.IFM.Domain.Trade.TradeBoundaryDiagnosticLog.Write18(logger, e, nameof(RiskObservationRecoveryService), nameof(SynchronizeAndAcknowledgeAsync), snapshot.WorkflowId);
        }
    }

    internal static long Advance(long after, IReadOnlyList<long> eventIds) => eventIds.Count == 0 ? after : Math.Max(after, eventIds.Max());
    public async Task SynchronizeAsync(WorkflowStrategyStateUpdatedEvent snapshot, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.TerminalRisk is not { } evidence)
            return;
        var id = new PortfolioFundId(evidence.PortfolioId, evidence.FundId);
        var aggregate = await (funds.LoadFundAsync(id, token)).ConfigureAwait(false);
        var order = aggregate.Composition(evidence.OrderId).Order;
        if (order.TerminalRisk == evidence)
            return;
        if (order.RiskAuthorization is not null)
            throw new InvalidOperationException("Fund authorization requires explicit financial reconciliation.");
        var command = new SynchronizeFundRiskOutcomeCommand
        {
            CommandId = RiskFinancialHandoff.Identity(evidence.SourceCommandId, $"FundTerminal/{order.AggregateVersion}"),
            EntityId = id,
            Subject = new(ActorType.Command, CreateFundMandateCommand.Actor, SynchronizeFundRiskOutcomeCommand.Verb, id.Format()),
            ErrorCode = 34100,
            CorrelationId = snapshot.CorrelationId,
            RequestedOnUtc = evidence.DecidedAtUtc,
            Access = PortfolioAccessContext.Workflow("RiskOutcomeRecovery"),
            ExpectedVersion = order.AggregateVersion,
            Evidence = evidence
        };
        var result = await (actors.RequestAsync<SynchronizeFundRiskOutcomeCommand, PortfolioFundId>(command, token)).ConfigureAwait(false);
        if (!result.Success)
            throw new InvalidOperationException(result.ErrorMessage);
        if ((await (funds.LoadFundAsync(id, token)).ConfigureAwait(false)).Composition(evidence.OrderId).Order.TerminalRisk != evidence)
            throw new InvalidOperationException("Fund outcome acknowledgement is not yet authoritative.");
    }
}
