using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.Schema;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.PredictiveModelDb.Schema;

public sealed class PredictiveModelSchemaDb(IDbConnectionSettings connectionSettings, ILogger<DbProvider> logger)
    : SchemaDbContext<PredictiveModelSchemaDb>(connectionSettings[PredictiveModelDbContext.PredictiveModelDbConnection], logger)
{
    static readonly SchemaObjectDefinition[] Objects =
    [
        new("futures_iti_trend_class_data", PredictiveModelSchemaCql.CreateFuturesItiTrendClassDataTable, "DROP TABLE IF EXISTS futures_iti_trend_class_data;"),
        new("futures_iti_trend_class_model", PredictiveModelSchemaCql.CreateFuturesItiTrendClassModelTable, "DROP TABLE IF EXISTS futures_iti_trend_class_model;"),
        new("futures_iti_trend_delta_data", PredictiveModelSchemaCql.CreateFuturesItiTrendDeltaDataTable, "DROP TABLE IF EXISTS futures_iti_trend_delta_data;"),
        new("futures_iti_trend_delta_model", PredictiveModelSchemaCql.CreateFuturesItiTrendDeltaModelTable, "DROP TABLE IF EXISTS futures_iti_trend_delta_model;"),
        new("request_id", PredictiveModelSchemaCql.CreateRequestIdTable, "DROP TABLE IF EXISTS request_id;")
    ];

    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => Objects;
}
