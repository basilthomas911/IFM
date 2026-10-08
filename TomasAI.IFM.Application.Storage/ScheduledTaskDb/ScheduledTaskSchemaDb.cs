using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.SchemaDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
namespace TomasAI.IFM.Application.Storage.ScheduledTaskDb;
/// <summary>Creates additive scheduled-task tables in the configured ScyllaDB database.</summary>
public sealed class ScheduledTaskSchemaDb(IDbConnectionSettings settings, ILogger<DbProvider> logger)
    : SchemaDbContext<ScheduledTaskSchemaDb>(settings["TradeDbConnection"], logger)
{
    private static readonly SchemaObjectDefinition[] Objects =
    [
        new("futures_completed_end_of_day", "CREATE TABLE IF NOT EXISTS futures_completed_end_of_day (environment text PRIMARY KEY, completed_value_date date, run_id uuid);", "DROP TABLE IF EXISTS futures_completed_end_of_day;"),
        new("scheduled_task_definition", "CREATE TABLE IF NOT EXISTS scheduled_task_definition (environment text, host_id text, schedule_id uuid, revision bigint, payload blob, PRIMARY KEY ((environment, host_id), schedule_id));", "DROP TABLE IF EXISTS scheduled_task_definition;"),
        new("scheduled_task_catalog", "CREATE TABLE IF NOT EXISTS scheduled_task_catalog (environment text, host_id text, revision bigint, payload blob, PRIMARY KEY ((environment, host_id)));", "DROP TABLE IF EXISTS scheduled_task_catalog;"),
        new("scheduled_task_run", "CREATE TABLE IF NOT EXISTS scheduled_task_run (environment text, host_id text, schedule_id uuid, intended_fire timestamp, run_id uuid, revision bigint, payload blob, PRIMARY KEY ((environment, host_id, schedule_id), intended_fire, run_id)) WITH CLUSTERING ORDER BY (intended_fire DESC, run_id ASC);", "DROP TABLE IF EXISTS scheduled_task_run;")
    ];
    /// <inheritdoc />
    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => Objects;
}
