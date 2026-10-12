using System.Text.Json;
using TomasAI.IFM.Storage.Tools;

// Scylla timestamps/dates are finite; preserve year-one values instead of converting them to PostgreSQL infinity.
AppContext.SetSwitch("Npgsql.DisableDateTimeInfinityConversions", true);

var targetConnection = Environment.GetEnvironmentVariable("IFM_OFFLINE_MIGRATION_TARGET")
    ?? throw new InvalidOperationException("Set IFM_OFFLINE_MIGRATION_TARGET to the isolated PostgreSQL staging database.");
if (args is ["--initialize-target"])
{
    await OfflineSchemaPreparation.RunAsync(targetConnection).ConfigureAwait(false);
    Console.WriteLine(JsonSerializer.Serialize(new { schemaPreparation = "passed" }));
    return;
}
if (args is ["--copy-sequences-writers-stopped"])
{
    Console.WriteLine(JsonSerializer.Serialize(await SequenceMigration.RunAsync(targetConnection).ConfigureAwait(false)));
    return;
}
if (args is ["--rebuild-regime-invocations"])
{
    Console.WriteLine(JsonSerializer.Serialize(await RegimeInvocationMigration.RunAsync(targetConnection).ConfigureAwait(false)));
    return;
}
if (args is not ["--copy-table-writers-stopped", var keyspace, var schema, var table])
    throw new ArgumentException("Usage: --copy-table-writers-stopped <source-keyspace> <target-schema> <table>.", nameof(args));
Console.WriteLine(JsonSerializer.Serialize(await OfflineTableCopy.RunAsync(targetConnection, keyspace, schema, table).ConfigureAwait(false)));
