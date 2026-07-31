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
        new("futures_contract", SecuritiesSchemaCql.CreateFuturesContractTable, "DROP TABLE IF EXISTS futures_contract;"),
        new("futures_option_contract", SecuritiesSchemaCql.CreateFuturesOptionContractTable, "DROP TABLE IF EXISTS futures_option_contract;")
    ];

    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => Objects;
}
