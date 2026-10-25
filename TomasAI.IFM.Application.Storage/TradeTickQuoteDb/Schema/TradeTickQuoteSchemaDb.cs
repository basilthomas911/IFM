using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.SchemaDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.TradeTickQuoteDb.Schema;

/// <summary>Creates the isolated raw trade and quote schema in dependency order.</summary>
/// <param name="settings">Named Scylla connection settings.</param>
/// <param name="logger">Schema execution diagnostics.</param>
public sealed class TradeTickQuoteSchemaDb(IDbConnectionSettings settings, ILogger<DbProvider> logger)
    : SchemaDbContext<TradeTickQuoteSchemaDb>(settings[TradeTickQuoteDbContext.TradeTickQuoteDbConnection], logger)
{
    /// <inheritdoc />
    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => TradeTickQuoteSchemaCql.Definitions;
}
