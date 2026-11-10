namespace TomasAI.IFM.Application.Storage.TradeDb;

/// <summary>PostgreSQL projection statements for recoverable strategy opening execution.</summary>
public static class StrategyExecutionHandoffSql
{
    public const string Create = """
        CREATE TABLE IF NOT EXISTS trade.strategy_execution_handoff (
            handoff_id text PRIMARY KEY, workflow_id uuid NOT NULL, revision bigint NOT NULL,
            phase integer NOT NULL, updated_at timestamptz NOT NULL, payload bytea NOT NULL);
        CREATE INDEX IF NOT EXISTS strategy_execution_handoff_workflow ON trade.strategy_execution_handoff(workflow_id);
        CREATE INDEX IF NOT EXISTS strategy_execution_handoff_pending ON trade.strategy_execution_handoff(handoff_id) WHERE phase<6;
        """;
    public const string Upsert = """
        INSERT INTO trade.strategy_execution_handoff(handoff_id,workflow_id,revision,phase,updated_at,payload)
        VALUES($1,$2,$3,$4,$5,$6) ON CONFLICT(handoff_id) DO UPDATE SET
            revision=EXCLUDED.revision,phase=EXCLUDED.phase,updated_at=EXCLUDED.updated_at,payload=EXCLUDED.payload
        WHERE strategy_execution_handoff.revision < EXCLUDED.revision;
        """;
    public const string Read = "SELECT payload FROM trade.strategy_execution_handoff WHERE handoff_id=$1;";
    public const string ReadWorkflow = "SELECT payload FROM trade.strategy_execution_handoff WHERE workflow_id=$1 ORDER BY handoff_id;";
    public const string Pending = "SELECT payload FROM trade.strategy_execution_handoff WHERE phase<6 AND handoff_id>$1 ORDER BY handoff_id LIMIT 32;";
    public const string Authorization = """
        SELECT e.eventstreamid,n.eventname,n.eventtypename,e.eventversion,e.eventpayload,e.commandid,e.eventtimestamp::text,e.streamversion
        FROM event_log e JOIN event_name_id n ON n.eventnameid=e.eventnameid
        WHERE e.eventversion=$1 AND e.commandid=$2;
        """;
}
