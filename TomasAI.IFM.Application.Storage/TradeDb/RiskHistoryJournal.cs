using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
namespace TomasAI.IFM.Application.Storage.TradeDb;

/// <summary>Bounded committed-source scans repeat from zero to discover late commits and repair missing projections.</summary>
public sealed class RiskHistoryJournal(IPostgresEventTransaction transactions)
{
    public Task<long> LoadCursorAsync(CancellationToken token=default,string projectionName="risk-history-v1")
        =>transactions.ExecuteAsync(async(db,ct)=>
        {
            await db.ExecuteAsync("CREATE TABLE IF NOT EXISTS risk_history_projection_progress (projection_name text PRIMARY KEY,after_event_version bigint NOT NULL CHECK(after_event_version>=0));",[],ct);
            var value=await db.ScalarAsync("SELECT after_event_version FROM risk_history_projection_progress WHERE projection_name=$1;",[projectionName],ct);
            return value is null ? 0 : Convert.ToInt64(value);
        },token);
    public Task SaveCursorAsync(long after,CancellationToken token=default,string projectionName="risk-history-v1")
        =>transactions.ExecuteAsync(async(db,ct)=>
        {
            await db.ExecuteAsync("INSERT INTO risk_history_projection_progress(projection_name,after_event_version) VALUES($2,$1) ON CONFLICT(projection_name) DO UPDATE SET after_event_version=EXCLUDED.after_event_version;",[after,projectionName],ct);
            return true;
        },token);
    public Task<IReadOnlyList<WorkflowStrategyStateUpdatedEvent>> PageAsync(long after, CancellationToken token = default)
        => transactions.ExecuteAsync(async (db, ct) =>
        {
            var rows = await db.QueryAsync("""
                SELECT e.eventstreamid,n.eventname,n.eventtypename,e.eventversion,e.eventdata::text,e.commandid,e.eventtimestamp::text,e.streamversion
                FROM event_log e JOIN event_name_id n ON n.eventnameid=e.eventnameid
                WHERE e.eventversion>$1 AND n.eventname='WorkflowStrategyStateUpdatedEvent'
                ORDER BY e.eventversion LIMIT 32;
                """, [after], Read, ct);
            return (IReadOnlyList<WorkflowStrategyStateUpdatedEvent>)rows.Select(x => (WorkflowStrategyStateUpdatedEvent)x.ToDomainEvent()).ToArray();
        }, token);

    public Task<IEvent?> ByCommandAsync(Guid command, CancellationToken token = default)
        => transactions.ExecuteAsync(async (db, ct) =>
        {
            var rows = await db.QueryAsync("""
                SELECT e.eventstreamid,n.eventname,n.eventtypename,e.eventversion,e.eventdata::text,e.commandid,e.eventtimestamp::text,e.streamversion
                FROM event_log e JOIN event_name_id n ON n.eventnameid=e.eventnameid
                WHERE e.commandid=$1 AND n.eventname IN ('WorkflowStrategyStateUpdatedEvent','RiskManagementFunctionCompletedEvent')
                ORDER BY e.eventversion DESC LIMIT 1;
                """, [command], Read, ct);
            return rows.FirstOrDefault()?.ToDomainEvent();
        }, token);
    static EventLogReadModel Read(System.Data.Common.DbDataReader r) => new(r.GetInt64(0),r.GetString(1),r.GetString(2),
        r.GetInt64(3),r.GetString(4),r.GetGuid(5),r.GetString(6),r.GetInt64(7));
}
