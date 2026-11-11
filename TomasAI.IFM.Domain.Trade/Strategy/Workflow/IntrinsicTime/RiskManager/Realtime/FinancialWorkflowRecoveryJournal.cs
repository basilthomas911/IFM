
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Realtime;

/// <summary>
/// Reads bounded pages of latest committed workflow snapshots. Repeated full passes, rather than
/// a global event-version watermark, also discover transactions that commit out of allocation order.
/// This journal never writes workflow or financial state and does not depend on Scylla projections.
/// </summary>
public sealed class FinancialWorkflowRecoveryJournal(IPortfolioDbReadContext database)
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1068", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task<FinancialWorkflowRecoveryPage> ReadPageAsync(long afterStreamId, CancellationToken token = default,
        string? exactStream = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(afterStreamId);
        var rows = await database.ReadFinancialWorkflowSourcesAsync(afterStreamId, exactStream, token)
            .ConfigureAwait(false);
            var snapshots = new List<WorkflowStrategyStateUpdatedEvent>(rows.Count);
            var invalid = new List<long>();
            foreach (var row in rows)
            {
                if (row.Event is null) continue;
                // Legacy/unsupported stream tails cannot be resumed by interpreting an older snapshot.
                if (!string.Equals(row.Event.EventName,nameof(WorkflowStrategyStateUpdatedEvent),StringComparison.Ordinal)) continue;
                try
                {
                    if (row.Event.ToDomainEvent() is not WorkflowStrategyStateUpdatedEvent snapshot || snapshot.State is null ||
                        snapshot.State.EntityId != snapshot.EntityId || snapshot.State.WorkflowId != snapshot.WorkflowId ||
                        snapshot.State.WorkflowRevision != snapshot.WorkflowRevision)
                        invalid.Add(row.Stream);
                    else snapshots.Add(snapshot);
                }
                catch (Newtonsoft.Json.JsonException) { invalid.Add(row.Stream); }
                catch (InvalidOperationException) { invalid.Add(row.Stream); }
                // Keep the stream pending for operator repair and a later full pass, without starving
                // unrelated streams or exposing the potentially sensitive malformed payload in logs.
            }
            return new FinancialWorkflowRecoveryPage(rows.Count == 0 ? 0 : rows[^1].Stream, rows.Count, snapshots)
            { InvalidStreamIds = invalid };
    }

}
