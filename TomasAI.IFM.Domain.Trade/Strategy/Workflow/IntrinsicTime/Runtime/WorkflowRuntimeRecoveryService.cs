using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Runtime;

/// <summary>Restart/outcome reconciliation is outside decisions; it never reruns a calculator or replaces an operation ID.</summary>
public sealed partial class WorkflowRuntimeRecoveryService(IPostgresEventTransaction transactions,
    WorkflowLocalDispatcher dispatcher, WorkflowResidentStateRegistry resident, IntrinsicTimeStrategyWorkflowOptions options,
    ILogger<WorkflowRuntimeRecoveryService> logger) : BackgroundService
{
    Guid after;
    bool startupScanComplete;
    readonly Dictionary<Guid, IntrinsicTimeStrategyWorkflowView> deferred = [];
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        do
        {
            // Disabling new starts must not abandon already-approved or uncertain financial operations.
            if (!options.LocalDecisionRuntimeEnabled || !dispatcher.ReadyForRecovery) continue;
            try
            {
                await ReconcileUncertainAsync(stoppingToken).ConfigureAwait(false);
                if (startupScanComplete)
                {
                    var pending = deferred.Values.Concat(resident.ActiveViews().Where(value => value.DecisionRuntimeVersion > 0
                        && value.EntryCommit is { Phase: WorkflowEntryCommitPhase.Approved or WorkflowEntryCommitPhase.OutcomeUnknown }))
                        .DistinctBy(value => value.WorkflowId).Take(64).ToArray();
                    foreach (var view in pending) await RecoverIndependentAsync(view, stoppingToken).ConfigureAwait(false);
                    continue;
                }
                var page = await transactions.ExecuteOnceAsync((db, token) => db.QueryAsync(
                    TradeDbWorkflowRuntimeSql.ReadActiveDecisionRecoveryPage, [after],
                    row => (Id: row.GetGuid(0), Payload: row.GetFieldValue<byte[]>(1)), token), stoppingToken).ConfigureAwait(false);
                foreach (var row in page)
                {
                    stoppingToken.ThrowIfCancellationRequested();
                    after = row.Id;
                    var view = MessagePackBinarySerializer.Shared.Deserialize<IntrinsicTimeStrategyWorkflowView>(row.Payload);
                    if (view is { DecisionRuntimeVersion: > 0 }) await RecoverIndependentAsync(view, stoppingToken).ConfigureAwait(false);
                }
                if (page.Count < 64) startupScanComplete = true;
            }
            catch (Exception error) when (!stoppingToken.IsCancellationRequested) { RecoveryUnconfirmed(logger, error); }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
    internal async Task ReconcileUncertainAsync(CancellationToken token)
    {
        await Parallel.ForEachAsync(resident.UncertainOutcomes(), new ParallelOptions
        { MaxDegreeOfParallelism = 4, CancellationToken = token }, async (unconfirmed, stopping) =>
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stopping);
            deadline.CancelAfter(TimeSpan.FromSeconds(5));
            try
            {
                var rows = await transactions.ExecuteOnceAsync(async (db, ct) =>
                {
                    await db.ExecuteAsync(TradeDbWorkflowRuntimeSql.LockDecisionStream, [unconfirmed.Stream], ct).ConfigureAwait(false);
                    return await db.QueryAsync(TradeDbWorkflowRuntimeSql.ReadDecisionOutcome, [unconfirmed.View.WorkflowId.Value],
                        row => row.GetFieldValue<byte[]>(0), ct).ConfigureAwait(false);
                }, deadline.Token).ConfigureAwait(false);
                if (rows.Count == 0) { resident.ConfirmOutcome(unconfirmed.View.WorkflowId.Value); return; }
                var saved = MessagePackBinarySerializer.Shared.Deserialize<IntrinsicTimeStrategyWorkflowView>(rows[0]);
                if (saved is null) return;
                if (saved.Status == WorkflowStrategyMachineStatus.Started && saved.EntryCommit is null)
                    await dispatcher.InterruptAsync(saved).WaitAsync(deadline.Token).ConfigureAwait(false);
                else await dispatcher.RecoverAsync(saved).WaitAsync(deadline.Token).ConfigureAwait(false);
                resident.ConfirmOutcome(unconfirmed.View.WorkflowId.Value);
            }
            catch (Exception error) when (!stopping.IsCancellationRequested) { RecoveryUnconfirmed(logger, error); }
        }).ConfigureAwait(false);
    }
    async Task RecoverIndependentAsync(IntrinsicTimeStrategyWorkflowView view, CancellationToken token)
    {
        try
        {
            await dispatcher.RecoverAsync(view).ConfigureAwait(false);
            deferred.Remove(view.WorkflowId.Value);
        }
        catch (Exception error) when (!token.IsCancellationRequested)
        {
            deferred[view.WorkflowId.Value] = view;
            RecoveryUnconfirmed(logger, error);
        }
    }
    [LoggerMessage(Level = LogLevel.Warning, Message = "Workflow runtime recovery could not confirm pending outcomes.")]
    static partial void RecoveryUnconfirmed(ILogger logger, Exception error);
}
