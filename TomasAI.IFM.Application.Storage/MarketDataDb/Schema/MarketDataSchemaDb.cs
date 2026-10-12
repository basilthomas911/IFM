using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.SchemaDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.MarketDataDb.Schema;

/// <summary>Creates and removes the complete PostgreSQL MarketDataDb schema in dependency order.</summary>
/// <param name="connectionSettings">The named PostgreSQL connection settings.</param>
/// <param name="logger">The database provider logger.</param>
public sealed class MarketDataSchemaDb(IDbConnectionSettings connectionSettings, ILogger<DbProvider> logger)
    : SchemaDbContext<MarketDataSchemaDb>(connectionSettings[MarketDataDbContext.MarketDataDbConnection], logger)
{
    /// <summary>Gets the dependency-ordered table and index definitions.</summary>
    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => MarketDataSchemaSql.GetDefinitions();
}
