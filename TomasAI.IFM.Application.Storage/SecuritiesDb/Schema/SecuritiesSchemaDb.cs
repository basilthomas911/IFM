using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.Schema;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb.Schema;

public sealed class SecuritiesSchemaDb(IDbConnectionSettings connectionSettings, ILogger<DbProvider> logger)
    : SchemaDbContext<SecuritiesSchemaDb>(connectionSettings[SecuritiesDbContext.SecuritiesDbConnection], logger)
{
    static readonly SchemaObjectDefinition[] Objects =
    [
        new("futures_contract_rollover", SecuritiesSchemaCql.CreateFuturesContractRolloverTable, "DROP TABLE IF EXISTS futures_contract_rollover;"),
        new("futures_contract", SecuritiesSchemaCql.CreateFuturesContractTable, "DROP TABLE IF EXISTS futures_contract;"),
        new("futures_option_contract", SecuritiesSchemaCql.CreateFuturesOptionContractTable, "DROP TABLE IF EXISTS futures_option_contract;"),
        new("futures_contract_by_symbol_v2", SecuritiesSchemaCql.CreateFuturesContractBySymbolV2Table, "DROP TABLE IF EXISTS futures_contract_by_symbol_v2;"),
        new("futures_option_contract_by_symbol_v2", SecuritiesSchemaCql.CreateFuturesOptionContractBySymbolV2Table, "DROP TABLE IF EXISTS futures_option_contract_by_symbol_v2;"),
        new("securities_projection_state_v3", SecuritiesSchemaCql.CreateSecuritiesProjectionStateV3Table, "DROP TABLE IF EXISTS securities_projection_state_v3;"),
        new("securities_symbol_projection_state_v3", SecuritiesSchemaCql.CreateSecuritiesSymbolProjectionStateV3Table, "DROP TABLE IF EXISTS securities_symbol_projection_state_v3;"),
        new("securities_projection_operation_v3", SecuritiesSchemaCql.CreateSecuritiesProjectionOperationV3Table, "DROP TABLE IF EXISTS securities_projection_operation_v3;"),
        new("securities_projection_operation_scope_v3", SecuritiesSchemaCql.CreateSecuritiesProjectionOperationScopeV3Table, "DROP TABLE IF EXISTS securities_projection_operation_scope_v3;")
    ];

    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => Objects;
}
