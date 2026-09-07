namespace TomasAI.IFM.Application.Storage.TradeDb.Schema;
public static class TradeSelectionSchemaCql
{
    public const string Invocation = """
CREATE TABLE IF NOT EXISTS trade_selection_invocation_event (
    workflow_id uuid,
    invocation_id uuid,
    source_sequence bigint,
    event_id uuid,
    portfolio_id int,
    fund_id int,
    target_horizon smallint,
    lifecycle_status tinyint,
    outcome tinyint,
    occurred_at_utc timestamp,
    reason_code text,
    selected_deployment_id uuid,
    selected_deployment_version int,
    selected_strategy_id uuid,
    selected_strategy_version int,
    selected_structure_id uuid,
    selected_structure_version int,
    selected_variant_id uuid,
    selected_variant_version int,
    candidate_set_sha256 text,
    binding_sha256 text,
    parameter_set_id uuid,
    parameter_version int,
    parameter_sha256 text,
    result_id uuid,
    result_sha256 text,
    result_payload blob,
    event_payload blob,
    PRIMARY KEY ((workflow_id, invocation_id), source_sequence)
) WITH CLUSTERING ORDER BY (source_sequence DESC);
""";
    public const string History = """
CREATE TABLE IF NOT EXISTS trade_selection_history_by_fund_date (
    portfolio_id int,
    fund_id int,
    value_date date,
    occurred_at_utc timestamp,
    workflow_id uuid,
    invocation_id uuid,
    event_id uuid,
    target_horizon smallint,
    outcome tinyint,
    reason_code text,
    result_id uuid,
    result_sha256 text,
    PRIMARY KEY ((portfolio_id, fund_id, value_date),
                 occurred_at_utc, workflow_id, invocation_id)
) WITH CLUSTERING ORDER BY
    (occurred_at_utc DESC, workflow_id ASC, invocation_id ASC);
""";
}
