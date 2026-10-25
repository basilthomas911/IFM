using Microsoft.Extensions.Logging;

using TomasAI.IFM.Application.Storage.SchemaDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.PortfolioDb.Schema;

/// <summary>Creates all Portfolio projection and financial tables after the event-log schema.</summary>
/// <param name="settings">Named database connections.</param>
/// <param name="logger">Database provider diagnostics.</param>
public sealed class PortfolioSchemaDb(IDbConnectionSettings settings, ILogger<DbProvider> logger)
    : SchemaDbContext<PortfolioSchemaDb>(settings[PortfolioDbContext.PortfolioDbConnection], logger)
{
    static readonly SchemaObjectDefinition[] Objects =
    [
        new("portfolio_schema", PortfolioSchemaSql.Portfolio, PortfolioSchemaSql.Drop),
        new("portfolio_financial", PortfolioSchemaSql.Financial, "DROP SCHEMA IF EXISTS portfolio_financial CASCADE;")
    ];
    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => Objects;
}
