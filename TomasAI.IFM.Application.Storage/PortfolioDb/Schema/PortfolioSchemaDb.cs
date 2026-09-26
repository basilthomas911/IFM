using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.SchemaDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.PortfolioDb.Schema;

public sealed class PortfolioSchemaDb(IDbConnectionSettings settings, ILogger<DbProvider> logger)
    : SchemaDbContext<PortfolioSchemaDb>(settings[PortfolioDbContext.PortfolioDbConnection], logger)
{
    static readonly SchemaObjectDefinition[] Objects =
    [
        new("portfolio_schema", PortfolioDbSql.Schema.Create, PortfolioDbSql.Schema.Drop)
    ];
    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => Objects;
}
