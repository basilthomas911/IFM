using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.Schema;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb.Schema;

public sealed class TradeSchemaDb(IDbConnectionSettings connectionSettings, ILogger<DbProvider> logger)
    : SchemaDbContext<TradeSchemaDb>(connectionSettings[TradeDbContext.TradeDbConnection], logger)
{
    static readonly SchemaObjectDefinition[] Objects =
    [
        new("option_leg", TradeSchemaCql.CreateOptionLegTable, "DROP TABLE IF EXISTS option_leg;"),
        new("option_leg_data", TradeSchemaCql.CreateOptionLegDataTable, "DROP TABLE IF EXISTS option_leg_data;"),
        new("option_trade", TradeSchemaCql.CreateOptionTradeTable, "DROP TABLE IF EXISTS option_trade;"),
        new("option_trade_spread_bar_data", TradeSchemaCql.CreateOptionTradeSpreadBarDataTable, "DROP TABLE IF EXISTS option_trade_spread_bar_data;"),
        new("option_trade_spread_data", TradeSchemaCql.CreateOptionTradeSpreadDataTable, "DROP TABLE IF EXISTS option_trade_spread_data;"),
        new("regime_discovery", TradeSchemaCql.CreateRegimeDiscoveryTable, "DROP TABLE IF EXISTS regime_discovery;"),
        new("market_condition", TradeSchemaCql.CreateMarketConditionTable, "DROP TABLE IF EXISTS market_condition;"),
        new("market_condition_by_fund", TradeSchemaCql.CreateMarketConditionByFundTable, "DROP TABLE IF EXISTS market_condition_by_fund;"),
        ..EvidenceColumns("market_condition"),
        ..EvidenceColumns("market_condition_by_fund"),
        new("intrinsic_time_strategy_workflow", TradeSchemaCql.CreateIntrinsicTimeStrategyWorkflowTable, "DROP TABLE IF EXISTS intrinsic_time_strategy_workflow;"),
        new("intrinsic_time_strategy_workflow_active", TradeSchemaCql.CreateIntrinsicTimeStrategyWorkflowActiveByEntityTable, "DROP TABLE IF EXISTS intrinsic_time_strategy_workflow_active;"),
        new("intrinsic_time_strategy_workflow_start_attempt", TradeSchemaCql.CreateIntrinsicTimeStrategyWorkflowStartAttemptByEntityTable, "DROP TABLE IF EXISTS intrinsic_time_strategy_workflow_start_attempt;"),
        new("intrinsic_time_strategy_workflow_timeline", TradeSchemaCql.CreateIntrinsicTimeStrategyWorkflowTimelineTable, "DROP TABLE IF EXISTS intrinsic_time_strategy_workflow_timeline;"),
        new("intrinsic_time_strategy_workflow_by_entity", TradeSchemaCql.CreateIntrinsicTimeStrategyWorkflowByEntityTable, "DROP TABLE IF EXISTS intrinsic_time_strategy_workflow_by_entity;"),
        new("intrinsic_time_strategy_workflow_by_status_day", TradeSchemaCql.CreateIntrinsicTimeStrategyWorkflowByStatusDayTable, "DROP TABLE IF EXISTS intrinsic_time_strategy_workflow_by_status_day;"),
        new("trade_fill", TradeSchemaCql.CreateTradeFillTable, "DROP TABLE IF EXISTS trade_fill;"),
        new("trade_fill_data", TradeSchemaCql.CreateTradeFillDataTable, "DROP TABLE IF EXISTS trade_fill_data;"),
        new("trade_limit", TradeSchemaCql.CreateTradeLimitTable, "DROP TABLE IF EXISTS trade_limit;"),
        new("trade_live_feed", TradeSchemaCql.CreateTradeLiveFeedTable, "DROP TABLE IF EXISTS trade_live_feed;"),
        new("trade_order", TradeSchemaCql.CreateTradeOrderTable, "DROP TABLE IF EXISTS trade_order;"),
        new("trade_placement_signal", TradeSchemaCql.CreateTradePlacementSignalTable, "DROP TABLE IF EXISTS trade_placement_signal;"),
        new("trade_plan", TradeSchemaCql.CreateTradePlanTable, "DROP TABLE IF EXISTS trade_plan;"),
        new("trade_plan_forward_loss_limit", TradeSchemaCql.CreateTradePlanForwardLossLimitTable, "DROP TABLE IF EXISTS trade_plan_forward_loss_limit;"),
        new("trade_plan_forward_loss_ratio", TradeSchemaCql.CreateTradePlanForwardLossRatioTable, "DROP TABLE IF EXISTS trade_plan_forward_loss_ratio;"),
        new("trade_position", TradeSchemaCql.CreateTradePositionTable, "DROP TABLE IF EXISTS trade_position;"),
        new("trade_position_state", TradeSchemaCql.CreateTradePositionStateTable, "DROP TABLE IF EXISTS trade_position_state;"),
        new("trade_type_limit", TradeSchemaCql.CreateTradeTypeLimitTable, "DROP TABLE IF EXISTS trade_type_limit;")
    ];

    static SchemaObjectDefinition[] EvidenceColumns(string table) =>
    [
        Column(table, "volatilityBehavior", "text"),
        Column(table, "liquidityQuality", "text"),
        Column(table, "dataQuality", "text"),
        Column(table, "upstreamAlignment", "text"),
        Column(table, "evidencePayload", "blob"),
        Column(table, "conflictingEvidencePayload", "blob"),
        Column(table, "blockingReasonsPayload", "blob"),
        Column(table, "reasonsPayload", "blob"),
        Column(table, "summaryText", "text")
    ];

    static SchemaObjectDefinition Column(string table, string column, string type) => new(
        $"{table}_{column}",
        $"ALTER TABLE {table} ADD {column} {type};",
        $"ALTER TABLE {table} DROP {column};",
        ["already exists", "conflicts with an existing column"]);

    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => Objects;
}
