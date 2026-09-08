namespace TomasAI.IFM.Application.Storage.TradeDb.Schema;
public static class OrderCompositionSchemaCql
{
    public const string Invocation="CREATE TABLE IF NOT EXISTS order_composition_invocation (workflow_id uuid,invocation_id uuid,result_hash text,input_hash text,payload blob,PRIMARY KEY ((workflow_id),invocation_id));";
    public const string History="CREATE TABLE IF NOT EXISTS order_composition_history (portfolio_id int,fund_id int,value_date date,evaluated_at_utc timestamp,invocation_id uuid,workflow_id uuid,event_id uuid,target_horizon smallint,outcome tinyint,reason_code text,result_id uuid,result_hash text,PRIMARY KEY ((portfolio_id,fund_id,value_date),evaluated_at_utc,invocation_id)) WITH CLUSTERING ORDER BY (evaluated_at_utc DESC,invocation_id ASC);";
}
