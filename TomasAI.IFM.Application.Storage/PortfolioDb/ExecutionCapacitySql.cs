namespace TomasAI.IFM.Application.Storage.PortfolioDb;

/// <summary>Atomic actual-execution capacity settlement and reconstruction under the financial fence.</summary>
public static class ExecutionCapacitySql
{
    public const string Create = """
        CREATE TABLE IF NOT EXISTS portfolio_financial.accepted_execution_capacity (
            order_id integer PRIMARY KEY, portfolio_id integer NOT NULL, fund_id integer NOT NULL,
            execution_id uuid NOT NULL, source_event_version bigint NOT NULL,
            financial_revision bigint NOT NULL, payload bytea NOT NULL);
        """;
    public const string Read = "SELECT payload FROM portfolio_financial.accepted_execution_capacity WHERE order_id=$1 FOR UPDATE;";
    public const string Write = """
        INSERT INTO portfolio_financial.accepted_execution_capacity(order_id,portfolio_id,fund_id,execution_id,source_event_version,financial_revision,payload)
        VALUES($1,$2,$3,$4,$5,$6,$7) ON CONFLICT(order_id) DO UPDATE SET
        source_event_version=EXCLUDED.source_event_version,financial_revision=EXCLUDED.financial_revision,payload=EXCLUDED.payload
        WHERE accepted_execution_capacity.source_event_version < EXCLUDED.source_event_version;
        """;
    public const string Source = """
        SELECT e.eventstreamid,n.eventname,n.eventtypename,e.eventversion,e.eventpayload,e.commandid,e.eventtimestamp::text,e.streamversion
        FROM event_log e JOIN event_name_id n ON n.eventnameid=e.eventnameid WHERE e.commandid=$1 ORDER BY e.eventversion LIMIT 65;
        """;
    public const string Approval = "SELECT definition_hash FROM portfolio.accepted_trade_order WHERE order_id=$1 AND portfolio_id=$2 AND fund_id=$3 AND operation_id=$4;";
    public const string Exposures = "SELECT scope_kind,scope_key,measure,unit,amount,method_version FROM portfolio.accepted_trade_order_capacity WHERE order_id=$1 AND portfolio_id=$2 AND fund_id=$3;";
    public const string PostedSources = """
        SELECT source_event_key,posting_purpose,source_content_hash FROM portfolio_financial.financial_source_receipt
        WHERE book_id=$1 AND source_system=$2 AND source_event_key=ANY($3);
        """;
}
