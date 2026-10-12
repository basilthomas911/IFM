using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.SchemaDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.OptionPricerDb.Schema;

/// <summary>Creates all option-pricer PostgreSQL tables, indexes and additive schema changes.</summary>
public sealed class OptionPricerSchemaDb(IDbConnectionSettings connectionSettings, ILogger<DbProvider> logger)
    : SchemaDbContext<OptionPricerSchemaDb>(connectionSettings[OptionPricerDbContext.ConnectionName], logger)
{
    static readonly SchemaObjectDefinition[] Objects =
    [new("option_pricer", OptionPricerSchemaSql.Create, "DROP SCHEMA IF EXISTS option_pricer CASCADE;")];

    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => Objects;
}
