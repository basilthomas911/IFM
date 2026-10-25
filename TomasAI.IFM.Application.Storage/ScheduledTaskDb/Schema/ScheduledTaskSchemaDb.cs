using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.SchemaDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.ScheduledTaskDb.Schema;
/// <summary>Creates all PostgreSQL scheduled-task tables and indexes additively.</summary>
/// <param name = "settings">Named PostgreSQL settings.</param>
/// <param name = "logger">The schema logger.</param>
public sealed class ScheduledTaskSchemaDb(IDbConnectionSettings settings, ILogger<DbProvider> logger) : SchemaDbContext<ScheduledTaskSchemaDb>(settings[ScheduledTaskDbContext.ConnectionName] ?? settings["TradeDbConnection"], logger)
{
    static readonly SchemaObjectDefinition[] Objects = [new("scheduled_task", ScheduledTaskSchemaSql.Create, ScheduledTaskSchemaSql.Drop)];
    /// <inheritdoc/>
    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => Objects;
}