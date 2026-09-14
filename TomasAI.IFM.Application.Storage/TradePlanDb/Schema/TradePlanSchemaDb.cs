using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.Schema;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.TradePlanDb.Schema;

public sealed class TradePlanSchemaDb(IDbConnectionSettings settings, ILogger<DbProvider> logger)
    : SchemaDbContext<TradePlanSchemaDb>(
        settings[Application.Storage.TradeDb.TradeDbContext.TradeDbConnection], logger)
{
    static readonly SchemaObjectDefinition[] Objects =
    [
        new("iron_condor_trade_plan_v1", TradePlanSchemaCql.IronCondor, "DROP TABLE IF EXISTS iron_condor_trade_plan_v1;"),
        new("vertical_spread_trade_plan_v1", TradePlanSchemaCql.VerticalSpread, "DROP TABLE IF EXISTS vertical_spread_trade_plan_v1;"),
        new("futures_trade_plan_v1", TradePlanSchemaCql.Futures, "DROP TABLE IF EXISTS futures_trade_plan_v1;"),
        new("position_trade_plan_activity_by_date_v1", TradePlanSchemaCql.ActivityByDate,
            "DROP TABLE IF EXISTS position_trade_plan_activity_by_date_v1;"),
        new("position_exit_workflow_v1", TradePlanSchemaCql.ExitWorkflow,
            "DROP TABLE IF EXISTS position_exit_workflow_v1;")
    ];

    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => Objects;
}
