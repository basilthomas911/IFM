using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.Schema;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.MarketDataServiceDb;

public sealed class MarketDataServiceSchemaDb(IDbConnectionSettings settings, ILogger<DbProvider> logger)
    : SchemaDbContext<MarketDataServiceSchemaDb>(settings[MarketDataServiceDbContext.MarketDataServiceDbConnection], logger)
{
    static readonly SchemaObjectDefinition[] Objects =
    [
        new("market_data_service", MarketDataServiceSchemaSql.CreateSchema, "DROP SCHEMA IF EXISTS market_data_service CASCADE;"),
        new("futures_rollover_contract_assignment", MarketDataServiceSchemaSql.CreateAssignments,
            "DROP TABLE IF EXISTS market_data_service.futures_rollover_contract_assignment;"),
        new("watchdog_status_log", MarketDataServiceSchemaSql.CreateWatchdogLog,
            "DROP TABLE IF EXISTS market_data_service.watchdog_status_log;"),
        new("stage4_subscription_intent", Subscriptions.Stage4SubscriptionSchemaSql.Create,
            "DROP TABLE IF EXISTS market_data_service.stage4_intent_outbox, market_data_service.stage4_authority_watermark, market_data_service.stage4_lease_identity, market_data_service.stage4_intent_operation, market_data_service.stage4_intent_current;"),
        new("composition_route_plan", Subscriptions.PostgresCompositionRoutePlanStore.CreateTable,
            "DROP TABLE IF EXISTS market_data_service.composition_route_plan;"),
        new("dataset_incident", MarketDataServiceSchemaSql.CreateDatasetIncidents,
            "DROP TABLE IF EXISTS market_data_service.dataset_incident_transition; DROP TABLE IF EXISTS market_data_service.dataset_incident_current;")
    ];
    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => Objects;
}
