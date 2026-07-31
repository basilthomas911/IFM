using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.Schema;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.LogDb.Schema;

public sealed class LogSchemaDb(IDbConnectionSettings connectionSettings, ILogger<DbProvider> logger)
    : SchemaDbContext<LogSchemaDb>(connectionSettings[LogDbContext.LogDbConnection], logger)
{
    static readonly SchemaObjectDefinition[] Objects =
    [
        new("telemetry_log", LogSchemaSql.CreateTelemetryLogTable, "DROP TABLE IF EXISTS public.telemetry_log;")
    ];

    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => Objects;
}
