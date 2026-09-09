using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Application.Storage.PortfolioFinancial;

public sealed record FinancialWorkflowRecoveryPage(long NextStreamId, int StreamsRead,
    IReadOnlyList<WorkflowStrategyStateUpdatedEvent> Snapshots)
{
    public IReadOnlyList<long> InvalidStreamIds { get; init; } = [];
}

/// <summary>
/// Reads bounded pages of latest committed workflow snapshots. Repeated full passes, rather than
/// a global event-version watermark, also discover transactions that commit out of allocation order.
/// This journal never writes workflow or financial state and does not depend on Scylla projections.
/// </summary>
public sealed class FinancialWorkflowRecoveryJournal(IPostgresEventTransaction transactions)
{
    public Task<FinancialWorkflowRecoveryPage> ReadPageAsync(long afterStreamId, CancellationToken token=default,
        string? exactStream=null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(afterStreamId);
        return transactions.ExecuteAsync(async(db,ct)=>
        {
            var rows=await db.QueryAsync("""
                WITH page AS (
                    SELECT eventstreamid FROM event_stream_id
                    WHERE eventstreamid>$1 AND eventstream LIKE 'Command.IntrinsicTimeStrategyWorkflowCommand.%'
                      AND ($2::text IS NULL OR eventstream=$2)
                    ORDER BY eventstreamid LIMIT 32
                )
                SELECT p.eventstreamid::bigint,n.eventname,n.eventtypename,e.eventversion,e.EventPayload,
                       e.commandid,e.eventtimestamp::text,e.streamversion
                FROM page p
                LEFT JOIN LATERAL (
                    SELECT * FROM event_log l WHERE l.eventstreamid=p.eventstreamid
                    ORDER BY streamversion DESC LIMIT 1
                ) e ON true
                LEFT JOIN event_name_id n ON n.eventnameid=e.eventnameid
                ORDER BY p.eventstreamid;
                """,[afterStreamId,exactStream],r=>(Stream:r.GetInt64(0),Event:r.IsDBNull(1)?null:
                    new EventLogReadModel(r.GetInt64(0),r.GetString(1),r.GetString(2),r.GetInt64(3),r.GetFieldValue<byte[]>(4),
                        r.GetGuid(5),r.GetString(6),r.GetInt64(7))),ct);
            var snapshots=new List<WorkflowStrategyStateUpdatedEvent>(rows.Count);
            var invalid=new List<long>();
            foreach(var row in rows)
            {
                if(row.Event is null) continue;
                // Legacy/unsupported stream tails cannot be resumed by interpreting an older snapshot.
                if(row.Event.EventName!=nameof(WorkflowStrategyStateUpdatedEvent)) continue;
                try
                {
                    if(row.Event.ToDomainEvent() is not WorkflowStrategyStateUpdatedEvent snapshot || snapshot.State is null ||
                        snapshot.State.EntityId!=snapshot.EntityId || snapshot.State.WorkflowId!=snapshot.WorkflowId ||
                        snapshot.State.WorkflowRevision!=snapshot.WorkflowRevision)
                        invalid.Add(row.Stream);
                    else snapshots.Add(snapshot);
                }
                catch(Newtonsoft.Json.JsonException) { invalid.Add(row.Stream); }
                catch(InvalidOperationException) { invalid.Add(row.Stream); }
                // Keep the stream pending for operator repair and a later full pass, without starving
                // unrelated streams or exposing the potentially sensitive malformed payload in logs.
            }
            return new FinancialWorkflowRecoveryPage(rows.Count==0?0:rows[^1].Stream,rows.Count,snapshots)
                { InvalidStreamIds=invalid };
        },token);
    }
}
