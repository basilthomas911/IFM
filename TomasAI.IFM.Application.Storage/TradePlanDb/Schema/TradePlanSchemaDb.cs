using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.SchemaDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.TradePlanDb.Schema;

public sealed class TradePlanSchemaDb(IDbConnectionSettings settings, ILogger<DbProvider> logger)
    : SchemaDbContext<TradePlanSchemaDb>(
        settings[TradePlanDbContext.TradePlanDbConnection], logger)
{
    static readonly SchemaObjectDefinition[] Objects =
    [
        new("iron_condor_trade_plan", TradePlanSchemaCql.IronCondor, "DROP TABLE IF EXISTS iron_condor_trade_plan;"),
        new("vertical_spread_trade_plan", TradePlanSchemaCql.VerticalSpread, "DROP TABLE IF EXISTS vertical_spread_trade_plan;"),
        new("futures_trade_plan", TradePlanSchemaCql.Futures, "DROP TABLE IF EXISTS futures_trade_plan;"),
        new("position_trade_plan_activity_by_date", TradePlanSchemaCql.ActivityByDate,
            "DROP TABLE IF EXISTS position_trade_plan_activity_by_date;"),
        new("position_exit_workflow", TradePlanSchemaCql.ExitWorkflow,
            "DROP TABLE IF EXISTS position_exit_workflow;")
    ];

    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => Objects;
}
