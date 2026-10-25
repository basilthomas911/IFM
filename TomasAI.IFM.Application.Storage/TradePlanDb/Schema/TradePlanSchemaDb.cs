using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.SchemaDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.TradePlanDb.Schema;

/// <summary>Creates PostgreSQL plan tables, risk scalar columns and ordered history indexes.</summary>
/// <param name="settings">Named PostgreSQL connection settings.</param>
/// <param name="logger">Schema execution diagnostics.</param>
public sealed class TradePlanSchemaDb(IDbConnectionSettings settings, ILogger<DbProvider> logger)
    : SchemaDbContext<TradePlanSchemaDb>(settings[TradePlanDbContext.TradePlanDbConnection], logger)
{
    static readonly SchemaObjectDefinition[] Objects =
        [new("trade_plan", TradePlanSchemaSql.Create, TradePlanSchemaSql.Drop)];

    /// <inheritdoc />
    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => Objects;
}
