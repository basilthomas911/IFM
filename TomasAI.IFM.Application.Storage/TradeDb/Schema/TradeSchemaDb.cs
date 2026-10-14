using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.SchemaDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb.Schema;
/// <summary>Creates all PostgreSQL trade tables, indexes and additive changes from the single DDL catalog.</summary>
public sealed class TradeSchemaDb(IDbConnectionSettings connectionSettings, ILogger<DbProvider> logger) : SchemaDbContext<TradeSchemaDb>(connectionSettings[TradeDbContext.TradeDbConnection], logger)
{
    static readonly SchemaObjectDefinition[] Objects = [new("trade", TradeSchemaSql.Create, TradeSchemaSql.Drop)];
    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => Objects;
}