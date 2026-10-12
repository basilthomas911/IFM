using System.Text.Json;
using TomasAI.IFM.Storage.Tools;
using System.Security.Cryptography;
using System.Text;
using Npgsql;
using TomasAI.IFM.Application.Storage.ConfigurationDb.Rebuild;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Shared.Storage;

if (args is ["--schema-inventory"])
{
    Console.WriteLine(SchemaInventory.Export());
    return;
}
// Explicit offline Development migration. Preparation adds schemas; no mode deletes source data or starts services.
if (args.Length != 1 || args[0] is not ("--capture-writers-stopped" or "--verify-existing-baseline" or "--prepare-development-baseline-writers-stopped" or "--verify-isolated-rebuild"))
    throw new ArgumentException("Usage: --prepare-development-baseline-writers-stopped, --capture-writers-stopped or --verify-existing-baseline, or --verify-isolated-rebuild.",nameof(args));
var connection = Environment.GetEnvironmentVariable("IFM_CONFIGURATION_BASELINE_CONNECTION")
    ?? throw new InvalidOperationException("Set IFM_CONFIGURATION_BASELINE_CONNECTION to the consolidated Development PostgreSQL database.");
var identity = new NpgsqlConnectionStringBuilder(connection);
if (identity.Host is not ("localhost" or "127.0.0.1") || (identity.Database is not ("event-source-dev-db" or "ifm-dev-db" or "ifm_storage_refactor_test") && !identity.Database.StartsWith("ifm_policy_migration_qualification_",StringComparison.Ordinal)))
    throw new InvalidOperationException("Configuration baseline migration is limited to the named local Development databases.");
var settings = new DbConnectionSettings().Add(EventSourceActorDbContext.EventSourceActorDbConnection, connection, "System.Data.Postgres");
var transactions = new PostgresEventTransaction(settings);
if (string.Equals(args[0],"--prepare-development-baseline-writers-stopped",StringComparison.Ordinal))
{
    var preparationSettings=new DbConnectionSettings().Add("ConfigurationDbConnection",connection,"System.Data.Postgres");
    var logger=Microsoft.Extensions.Logging.Abstractions.NullLogger<TomasAI.IFM.Framework.Storage.DbProvider>.Instance;
    await TomasAI.IFM.Application.Storage.ConfigurationDb.Schema.ConfigurationSchemaDb.WithoutLookupDefaults(preparationSettings,logger)
        .CreateAllAsync().ConfigureAwait(false);
    Console.WriteLine(JsonSerializer.Serialize(new {database=identity.Database,policyMigration=await OptionChainPolicyMigration.RunAsync(transactions).ConfigureAwait(false)}));
    return;
}
var baseline = new ConfigurationProjectionBaseline(transactions);
var existing = await baseline.ReadLatestAsync().ConfigureAwait(false);
if (string.Equals(args[0],"--capture-writers-stopped",StringComparison.Ordinal))
{
    if (existing is not null) throw new InvalidOperationException("A configuration baseline already exists; verify it before creating another.");
    var captured = await baseline.CaptureAsync(true).ConfigureAwait(false);
    existing = await baseline.ReadLatestAsync().ConfigureAwait(false) ?? throw new InvalidDataException("Committed baseline is not readable from source bytes.");
    if (captured.Id != existing.Id || !captured.Tables.SequenceEqual(existing.Tables))
        throw new InvalidDataException("Persisted configuration baseline differs from captured facts.");
}
if (existing is null) throw new InvalidDataException("No committed configuration baseline exists.");
if (string.Equals(args[0],"--verify-isolated-rebuild",StringComparison.Ordinal))
    await IsolatedConfigurationVerifier.RunAsync(existing).ConfigureAwait(false);
var invalid = existing.Tables.FirstOrDefault(table => !string.Equals(
    Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(table.RowsJson))),table.Sha256,StringComparison.Ordinal));
if (invalid is not null) throw new InvalidDataException("Persisted configuration baseline hash does not match " + invalid.Name);
static int CountRows(string json)
{
    using var document = JsonDocument.Parse(json);
    return document.RootElement.GetArrayLength();
}
// Print only proof metadata, not configuration payloads, credentials or database connection strings.
Console.WriteLine(JsonSerializer.Serialize(new { database = identity.Database, baselineId = existing.Id,
    sourceEventId = existing.EventId, sourceCut = existing.SourceCut, schemaVersion = existing.SchemaVersion,
    tables = existing.Tables.Select(x => new { table = x.Name, rows = CountRows(x.RowsJson), sha256 = x.Sha256 }),
    lookupSequence = existing.LookupSequenceLastValue, verifiedSourceBytesAtUtc = DateTime.UtcNow }));
