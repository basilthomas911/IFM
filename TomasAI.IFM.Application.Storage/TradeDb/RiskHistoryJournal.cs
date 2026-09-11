using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
namespace TomasAI.IFM.Application.Storage.TradeDb;

/// <summary>Receipt-based committed history projection, including events whose transactions commit after a newer sequence.</summary>
public sealed class RiskHistoryJournal(IPostgresEventTransaction transactions)
{
    public const string HistoryProjection = "risk-history-v1";
    public const string FundOutcomeProjection = "risk-fund-outcomes-v1";
    public const string CreateTables = """
        CREATE TABLE IF NOT EXISTS risk_history_projection_progress (
          projection_name text PRIMARY KEY,
          after_event_version bigint NOT NULL CHECK(after_event_version>=0));
        CREATE TABLE IF NOT EXISTS risk_history_projection_receipt (
          projection_name text NOT NULL,
          event_id bigint NOT NULL,
          projected_at_utc timestamptz NOT NULL DEFAULT now(),
          PRIMARY KEY(projection_name,event_id));
        CREATE TABLE IF NOT EXISTS risk_history_projection_issue (
          projection_name text NOT NULL,
          event_id bigint NOT NULL,
          workflow_id uuid NOT NULL,
          invocation_id uuid NOT NULL,
          source_revision bigint NOT NULL,
          issue_code text NOT NULL,
          stored_hash text NOT NULL,
          incoming_hash text NOT NULL,
          detected_at_utc timestamptz NOT NULL DEFAULT now(),
          PRIMARY KEY(projection_name,event_id));
        """;

    public Task<long> LoadCursorAsync(CancellationToken token=default,string projectionName=HistoryProjection)
        =>transactions.ExecuteAsync(async(db,ct)=>
        {
            await db.ExecuteAsync(CreateTables,[],ct);
            var value=await db.ScalarAsync("SELECT after_event_version FROM risk_history_projection_progress WHERE projection_name=$1;",[projectionName],ct);
            var cursor=value is null?0:Convert.ToInt64(value);
            // Compatible migration: the old cursor proves these events were previously projected.
            await db.ExecuteAsync("""
                INSERT INTO risk_history_projection_receipt(projection_name,event_id)
                SELECT $1,e.eventversion
                FROM event_log e JOIN event_name_id n ON n.eventnameid=e.eventnameid
                WHERE n.eventname='WorkflowStrategyStateUpdatedEvent' AND e.eventversion<=$2
                ON CONFLICT DO NOTHING;
                """,[projectionName,cursor],ct);
            return cursor;
        },token);

    public Task SaveCursorAsync(long after,CancellationToken token=default,string projectionName=HistoryProjection)
        =>transactions.ExecuteAsync(async(db,ct)=>
        {
            await db.ExecuteAsync("""
                INSERT INTO risk_history_projection_progress(projection_name,after_event_version) VALUES($2,$1)
                ON CONFLICT(projection_name) DO UPDATE
                SET after_event_version=GREATEST(risk_history_projection_progress.after_event_version,EXCLUDED.after_event_version);
                """,[after,projectionName],ct);
            return true;
        },token);

    public Task<IReadOnlyList<WorkflowStrategyStateUpdatedEvent>> PageAsync(long after, CancellationToken token = default,
        string projectionName=HistoryProjection)
        => transactions.ExecuteAsync(async (db, ct) =>
        {
            var rows = await db.QueryAsync("""
                SELECT e.eventstreamid,n.eventname,n.eventtypename,e.eventversion,e.EventPayload,e.commandid,e.eventtimestamp::text,e.streamversion
                FROM event_log e JOIN event_name_id n ON n.eventnameid=e.eventnameid
                WHERE n.eventname='WorkflowStrategyStateUpdatedEvent'
                  AND NOT EXISTS(
                    SELECT 1 FROM risk_history_projection_receipt r
                    WHERE r.projection_name=$1 AND r.event_id=e.eventversion)
                ORDER BY e.eventversion LIMIT 32;
                """, [projectionName], Read, ct);
            return (IReadOnlyList<WorkflowStrategyStateUpdatedEvent>)rows.Select(x => (WorkflowStrategyStateUpdatedEvent)x.ToDomainEvent()).ToArray();
        }, token);

    public Task AcknowledgeAsync(string projectionName,long eventId,CancellationToken token=default)
        =>transactions.ExecuteAsync(async(db,ct)=>
        {
            await db.ExecuteAsync("INSERT INTO risk_history_projection_receipt(projection_name,event_id) VALUES($1,$2) ON CONFLICT DO NOTHING;",[projectionName,eventId],ct);
            return true;
        },token);

    public Task QuarantineConflictAsync(long eventId,RiskHistoryProjectionResult result,CancellationToken token=default)
        =>transactions.ExecuteAsync(async(db,ct)=>
        {
            if(result.Disposition!=RiskHistoryProjectionDisposition.Conflict || result.WorkflowId==Guid.Empty || result.InvocationId==Guid.Empty)
                throw new ArgumentException("A complete Risk history conflict result is required.",nameof(result));
            await db.ExecuteAsync("""
                INSERT INTO risk_history_projection_issue(
                  projection_name,event_id,workflow_id,invocation_id,source_revision,issue_code,stored_hash,incoming_hash)
                VALUES($1,$2,$3,$4,$5,'ImmutableRevisionConflict',$6,$7)
                ON CONFLICT DO NOTHING;
                """,[HistoryProjection,eventId,result.WorkflowId,result.InvocationId,result.Revision,result.StoredHash!,result.IncomingHash!],ct);
            await db.ExecuteAsync(
                "INSERT INTO risk_history_projection_receipt(projection_name,event_id) VALUES($1,$2) ON CONFLICT DO NOTHING;",
                [HistoryProjection,eventId],ct);
            return true;
        },token);

    public Task<IEvent?> ByCommandAsync(Guid command, CancellationToken token = default)
        => transactions.ExecuteAsync(async (db, ct) =>
        {
            var rows = await db.QueryAsync("""
                SELECT e.eventstreamid,n.eventname,n.eventtypename,e.eventversion,e.EventPayload,e.commandid,e.eventtimestamp::text,e.streamversion
                FROM event_log e JOIN event_name_id n ON n.eventnameid=e.eventnameid
                WHERE e.commandid=$1 AND n.eventname IN ('WorkflowStrategyStateUpdatedEvent','RiskManagementFunctionCompletedEvent')
                ORDER BY e.eventversion DESC LIMIT 1;
                """, [command], Read, ct);
            return rows.FirstOrDefault()?.ToDomainEvent();
        }, token);

    static EventLogReadModel Read(System.Data.Common.DbDataReader r) => new(r.GetInt64(0),r.GetString(1),r.GetString(2),
        r.GetInt64(3),r.GetFieldValue<byte[]>(4),r.GetGuid(5),r.GetString(6),r.GetInt64(7));
}
