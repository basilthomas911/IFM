using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.SchemaDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.MarketDataServiceDb.Schema;

/// <summary>Creates and drops the ordered PostgreSQL schema catalog.</summary>
public sealed class MarketDataServiceSchemaDb(IDbConnectionSettings settings, ILogger<DbProvider> logger)
    : SchemaDbContext<MarketDataServiceSchemaDb>(settings[MarketDataServiceDbContext.MarketDataServiceDbConnection], logger)
{
    static readonly SchemaObjectDefinition[] Objects =
    [
        new("market_data_service", MarketDataServiceSchemaSql.CreateSchema, MarketDataServiceSchemaSql.DropSchema),
        new("futures_rollover_contract_assignment", MarketDataServiceSchemaSql.CreateAssignments,
            MarketDataServiceSchemaSql.DropAssignments),
        new("marketdata_recovery_log", MarketDataServiceSchemaSql.CreateRecoveryLog, MarketDataServiceSchemaSql.DropRecoveryLog),
        new("watchdog_status_log", MarketDataServiceSchemaSql.CreateWatchdogLog,
            MarketDataServiceSchemaSql.DropWatchdogLog),
        new("stage4_subscription_intent", MarketDataServiceSchemaSql.CreateDurableSubscriptions,
            MarketDataServiceSchemaSql.DropDurableSubscriptions),
        new("composition_route_plan", MarketDataServiceSchemaSql.CreateCompositionRoutePlan,
            MarketDataServiceSchemaSql.DropCompositionRoutePlan),
        new("dataset_incident", MarketDataServiceSchemaSql.CreateDatasetIncidents,
            MarketDataServiceSchemaSql.DropDatasetIncidents)
    ];
    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => Objects;
}
