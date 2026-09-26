using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.SchemaDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.ReferenceDb.Schema;

public sealed class ReferenceSchemaDb(IDbConnectionSettings connectionSettings, ILogger<DbProvider> logger)
    : SchemaDbContext<ReferenceSchemaDb>(connectionSettings[ReferenceDbContext.ReferenceDbConnection], logger)
{
    static readonly SchemaObjectDefinition[] Objects =
    [
        new("option_pricing_convention", OptionPricingConventionStore.CreateTable, "DROP TABLE IF EXISTS option_pricing_convention;"),
        new("option_pricing_reference_bundle", OptionPricingReferenceBundleStore.CreateTable, "DROP TABLE IF EXISTS option_pricing_reference_bundle;"),
        new("instrument_definition", InstrumentDefinitionStore.CreateTable, "DROP TABLE IF EXISTS instrument_definition;"),
        new("instrument_definition_product", InstrumentDefinitionStore.CreateProductTable, "DROP TABLE IF EXISTS instrument_definition_product;"),
        new("instrument_definition_snapshot", InstrumentDefinitionStore.CreateSnapshotTable, "DROP TABLE IF EXISTS instrument_definition_snapshot;"),
        new("instrument_definition_selection", InstrumentDefinitionStore.CreateSelectionTable, "DROP TABLE IF EXISTS instrument_definition_selection;"),
        new("instrument_definition_selection_status", InstrumentDefinitionStore.CreateSelectionStatusTable, "DROP TABLE IF EXISTS instrument_definition_selection_status;"),
        new("trade_strategy_symbol", TradeStrategySymbolStore.CreateTable, "DROP TABLE IF EXISTS trade_strategy_symbol;"),
        new("trade_strategy_family_catalog", TradeStrategyFamilyCatalogStore.CreateTable, "DROP TABLE IF EXISTS trade_strategy_family_catalog;"),
        new("reference_projection_state", ReferenceSchemaCql.CreateReferenceProjectionStateV3Table, "DROP TABLE IF EXISTS reference_projection_state;"),
        new("reference_projection_mutation", ReferenceSchemaCql.CreateReferenceProjectionMutationV3Table, "DROP TABLE IF EXISTS reference_projection_mutation;"),
        new("reference_projection_ownership", ReferenceSchemaCql.CreateReferenceProjectionOwnershipV3Table, "DROP TABLE IF EXISTS reference_projection_ownership;"),
        new("lookup_type", ReferenceSchemaCql.CreateLookupTypeTable, "DROP TABLE IF EXISTS lookup_type;"),
        new("mdi_forward_loss_ratio", ReferenceSchemaCql.CreateMDIForwardLossRatioTable, "DROP TABLE IF EXISTS mdi_forward_loss_ratio;"),
        new("scheduled_job_days", ReferenceSchemaCql.CreateScheduledJobDaysTable, "DROP TABLE IF EXISTS scheduled_job_days;"),
        new("scheduled_job", ReferenceSchemaCql.CreateScheduledJobTable, "DROP TABLE IF EXISTS scheduled_job;"),
        new("scheduled_job_by_name", ReferenceSchemaCql.CreateScheduledJobByNameV3Table, "DROP TABLE IF EXISTS scheduled_job_by_name;"),
        new("scheduled_job_write_ownership", ReferenceSchemaCql.CreateScheduledJobWriteOwnershipV3Table, "DROP TABLE IF EXISTS scheduled_job_write_ownership;"),
        new("trade_strategy_family", ReferenceSchemaCql.CreateTradeStrategyFamilyTable, "DROP TABLE IF EXISTS trade_strategy_family;")
    ];

    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => Objects;
}
