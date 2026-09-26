using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.SchemaDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb.Schema;

public sealed class SecuritiesSchemaDb(IDbConnectionSettings connectionSettings, ILogger<DbProvider> logger)
    : SchemaDbContext<SecuritiesSchemaDb>(connectionSettings[SecuritiesDbContext.SecuritiesDbConnection], logger)
{
    static readonly SchemaObjectDefinition[] Objects =
    [
        new("option_contract_expiry_calendar", SecuritiesSchemaCql.CreateOptionContractExpiryCalendarTable, "DROP TABLE IF EXISTS option_contract_expiry_calendar;"),
        new("option_contract_expiry_calendar_definition_columns", SecuritiesSchemaCql.AddOptionExpiryDefinitionColumns,
            "ALTER TABLE option_contract_expiry_calendar DROP underlyingContractId;",
            ["conflicts with an existing column", "already exists"]),
        new("option_contract_expiry_calendar_state", SecuritiesSchemaCql.CreateOptionContractExpiryCalendarStateTable, "DROP TABLE IF EXISTS option_contract_expiry_calendar_state;"),
        new("securities_reference_identity", SecuritiesDbCql.CreateReferenceIdentityTable, "DROP TABLE IF EXISTS securities_reference_identity;"),
        new("securities_reference_version", SecuritiesDbCql.CreateReferenceVersionTable, "DROP TABLE IF EXISTS securities_reference_version;"),
        new("futures_contract_rollover", SecuritiesSchemaCql.CreateFuturesContractRolloverTable, "DROP TABLE IF EXISTS futures_contract_rollover;"),
        new("futures_contract", SecuritiesSchemaCql.CreateFuturesContractTable, "DROP TABLE IF EXISTS futures_contract;"),
        new("futures_option_contract", SecuritiesSchemaCql.CreateFuturesOptionContractTable, "DROP TABLE IF EXISTS futures_option_contract;"),
        new("futures_contract_by_symbol", SecuritiesSchemaCql.CreateFuturesContractBySymbolV3Table, "DROP TABLE IF EXISTS futures_contract_by_symbol;"),
        new("futures_option_contract_by_symbol", SecuritiesSchemaCql.CreateFuturesOptionContractBySymbolV2Table, "DROP TABLE IF EXISTS futures_option_contract_by_symbol;"),
        new("securities_projection_state", SecuritiesSchemaCql.CreateSecuritiesProjectionStateV3Table, "DROP TABLE IF EXISTS securities_projection_state;"),
        new("securities_symbol_projection_state", SecuritiesSchemaCql.CreateSecuritiesSymbolProjectionStateV3Table, "DROP TABLE IF EXISTS securities_symbol_projection_state;"),
        new("securities_projection_operation", SecuritiesSchemaCql.CreateSecuritiesProjectionOperationV3Table, "DROP TABLE IF EXISTS securities_projection_operation;"),
        new("securities_projection_operation_scope", SecuritiesSchemaCql.CreateSecuritiesProjectionOperationScopeV3Table, "DROP TABLE IF EXISTS securities_projection_operation_scope;"),
        ReferencePayload("futures_contract"),
        ReferencePayload("futures_contract_by_symbol"),
        ReferencePayload("futures_option_contract"),
        ReferencePayload("futures_option_contract_by_symbol")
    ];

    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => Objects;

    static SchemaObjectDefinition ReferencePayload(string table) => new(
        table + "_reference_payload",
        $"ALTER TABLE {table} ADD referencePayload blob;",
        $"ALTER TABLE {table} DROP referencePayload;",
        ["conflicts with an existing column", "already exists"]);
}
